using System.IO;
using System.Text.Json.Nodes;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Infrastructure.GameData;

namespace SynthRidersPlaylistManager.Tests;

public sealed class BlacklistTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "srpm-blacklist-" + Guid.NewGuid().ToString("N"));
    private string Settings => Path.Combine(_root, "twitchsettings.bin");
    public BlacklistTests() { Directory.CreateDirectory(_root); File.WriteAllText(Settings, "{\"Blacklist\":[\"existing.synth\"],\"unknown\":{\"nested\":[1,null,true,\"保持\"]},\"volume\":0.73}"); }

    [Fact]
    public void ReadUsesExactFileName()
    {
        var result = SongFlagsStore.ReadBlacklist(_root);
        Assert.Contains("existing.synth", result);
        Assert.DoesNotContain("Existing.synth", result);
    }

    [Fact]
    public void BulkOnOffIsIdempotentAndPreservesUnknownJsonAndBackup()
    {
        var before = File.ReadAllBytes(Settings);
        var original = JsonNode.Parse(before)!;
        var store = new SongFlagsStore(() => true);
        var saved = store.SetBlacklist(_root, ["one.synth", "two.synth", "one.synth"], true);
        Assert.Equal(3, saved.Count);
        var backup = Assert.Single(Directory.GetFiles(_root, "*.bak"));
        Assert.Equal(before, File.ReadAllBytes(backup));
        var after = JsonNode.Parse(File.ReadAllText(Settings))!;
        Assert.True(JsonNode.DeepEquals(original["unknown"], after["unknown"]));
        Assert.True(JsonNode.DeepEquals(original["volume"], after["volume"]));
        var bytes = File.ReadAllBytes(Settings);
        store.SetBlacklist(_root, ["one.synth", "two.synth"], true);
        store.SetBlacklist(_root, ["absent.synth"], false);
        Assert.Equal(bytes, File.ReadAllBytes(Settings));
        Assert.Single(Directory.GetFiles(_root, "*.bak"));
        saved = store.SetBlacklist(_root, ["one.synth", "two.synth"], false);
        Assert.Equal("existing.synth", Assert.Single(saved));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void GameRunningRejectsWriteWithoutBackupOrChanges()
    {
        var before = File.ReadAllBytes(Settings);
        Assert.Throws<InvalidOperationException>(() => new SongFlagsStore(() => false).SetBlacklist(_root, ["one.synth"], true));
        Assert.Equal(before, File.ReadAllBytes(Settings));
        Assert.Empty(Directory.GetFiles(_root, "*.bak"));
    }

    [Fact]
    public void GameStartingBeforeReplaceLeavesOriginalAndRecoverableBackup()
    {
        var before = File.ReadAllBytes(Settings);
        var checks = 0;
        Assert.Throws<InvalidOperationException>(() => new SongFlagsStore(() => ++checks < 3).SetBlacklist(_root, ["one.synth"], true));
        Assert.Equal(before, File.ReadAllBytes(Settings));
        Assert.Equal(before, File.ReadAllBytes(Assert.Single(Directory.GetFiles(_root, "*.bak"))));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void ConcurrentExternalChangeIsNotOverwritten()
    {
        var checks = 0;
        var external = "{\"Blacklist\":[],\"external\":true}";
        var store = new SongFlagsStore(() => { if (++checks == 3) File.WriteAllText(Settings, external); return true; });
        Assert.Throws<IOException>(() => store.SetBlacklist(_root, ["one.synth"], true));
        Assert.Equal(external, File.ReadAllText(Settings));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Theory]
    [InlineData("{\"Blacklist\":[2]}")]
    [InlineData("{\"Blacklist\":null}")]
    [InlineData("{}")]
    public void InvalidArrayIsNeverReinitialized(string json)
    {
        File.WriteAllText(Settings, json);
        Assert.Throws<InvalidDataException>(() => new SongFlagsStore(() => true).SetBlacklist(_root, ["one.synth"], true));
        Assert.Equal(json, File.ReadAllText(Settings));
    }

    [Fact]
    public void ReplaceFailureKeepsOriginalAndCleansTemporaryFile()
    {
        var before = File.ReadAllBytes(Settings);
        using var locked = new FileStream(Settings, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Throws<IOException>(() => new SongFlagsStore(() => true).SetBlacklist(_root, ["one.synth"], true));
        Assert.Equal(before, File.ReadAllBytes(Settings));
        Assert.Equal(before, File.ReadAllBytes(Assert.Single(Directory.GetFiles(_root, "*.bak"))));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothPaneBlacklistCommandsShareStateButNotChecksOrSelection(bool fromA)
    {
        using var vm = ProductionViewModelFixture.Create();
        var source = fromA ? vm.PaneA : vm.PaneB;
        var other = fromA ? vm.PaneB : vm.PaneA;
        source.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs);
        other.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.Blacklist);
        var songs = vm.Songs.Where(x => x.Identity.Kind == SongKind.Custom).Take(2).ToArray();
        source.SelectedSong = songs[0];
        other.SelectedSong = songs[1];
        foreach (var song in songs) source.ToggleCheckedCommand.Execute(song);
        Assert.True(source.AddBlacklistCommand.CanExecute(null));
        source.AddBlacklistCommand.Execute(null);
        foreach (var song in songs)
        {
            Assert.True(song.IsBlacklisted);
            Assert.Equal(1, song.RowOpacity);
            Assert.DoesNotContain(song, source.VisibleSongs.Cast<SongItemViewModel>());
            Assert.Same(song, other.VisibleSongs.Cast<SongItemViewModel>().Single(s => s.Identity == song.Identity));
            Assert.True(source.IsSongChecked(song));
            Assert.False(other.IsSongChecked(song));
            Assert.True(other.ToggleCheckedCommand.CanExecute(song));
            other.ToggleCheckedCommand.Execute(song);
        }
        Assert.Same(songs[0], source.SelectedSong);
        Assert.Same(songs[1], other.SelectedSong);
        Assert.Same(songs[1], vm.SelectedSong);
        other.RemoveBlacklistCommand.Execute(null);
        Assert.Empty(other.VisibleSongs.Cast<object>());
        Assert.All(songs, song => { Assert.False(song.IsBlacklisted); Assert.Equal(1, song.RowOpacity); Assert.Contains(song, source.VisibleSongs.Cast<SongItemViewModel>()); });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FavoritesRefreshBothPanesWithoutChangingChecks(bool fromA)
    {
        using var vm = ProductionViewModelFixture.Create();
        vm.PaneA.SelectedCollection = vm.FavoritesNavigation[0];
        vm.PaneB.SelectedCollection = vm.FavoritesNavigation[0];
        var pane = fromA ? vm.PaneA : vm.PaneB;
        var song = vm.Songs.First(x => x.IsFavorite);
        pane.ToggleCheckedCommand.Execute(song);
        pane.ToggleFavoriteCommand.Execute(song);
        Assert.False(song.IsFavorite);
        Assert.DoesNotContain(song, vm.PaneA.VisibleSongs.Cast<SongItemViewModel>());
        Assert.DoesNotContain(song, vm.PaneB.VisibleSongs.Cast<SongItemViewModel>());
        pane.FavoriteOnCommand.Execute(null);
        Assert.Same(song, vm.PaneA.VisibleSongs.Cast<SongItemViewModel>().Single(x => x == song));
        Assert.Same(song, vm.PaneB.VisibleSongs.Cast<SongItemViewModel>().Single(x => x == song));
        Assert.True(pane.IsSongChecked(song));
    }

    [Fact]
    public void GameRunningBlocksBlacklistCommandsButNotChecks()
    {
        using var vm = ProductionViewModelFixture.Create();
        var song = vm.Songs.First(x => x.Identity.Kind == SongKind.Custom);
        vm.PaneA.ToggleCheckedCommand.Execute(song);
        vm.GameState = GameAccessState.RunningReadOnly;
        Assert.False(vm.PaneA.AddBlacklistCommand.CanExecute(null));
        vm.PaneA.AddBlacklistCommand.Execute(null);
        Assert.False(song.IsBlacklisted);
        Assert.True(vm.PaneA.ToggleCheckedCommand.CanExecute(song));
    }

    [Fact]
    public void BlacklistedSongRemainsSelectableAndCheckableButCannotBeAddedOrDraggedUntilRemoved()
    {
        using var vm = ProductionViewModelFixture.Create();
        var source = vm.PaneA;
        var target = vm.PaneB;
        source.SelectedCollection = vm.SmartNavigation.Single(item => item.Filter == NavigationFilter.AllSongs);
        target.SelectedCollection = vm.PlaylistNavigation.Single(item => item.PlaylistName == "Test Playlist A");
        var song = vm.Songs.Single(item => item.PlaylistNames.Contains("Test Playlist B") && !item.PlaylistNames.Contains("Test Playlist A"));

        source.ToggleCheckedCommand.Execute(song);
        source.AddBlacklistCommand.Execute(null);
        source.SelectedCollection = vm.SmartNavigation.Single(item => item.Filter == NavigationFilter.Blacklist);
        source.SelectedSong = song;

        Assert.Same(song, Assert.Single(source.VisibleSongs.Cast<SongItemViewModel>()));
        Assert.Same(song, source.SelectedSong);
        Assert.True(source.IsSongChecked(song));
        Assert.Equal(1, song.RowOpacity);

        source.ClearSelectionCommand.Execute(null);
        Assert.False(source.IsSongChecked(song));
        source.ToggleCheckedCommand.Execute(song);
        Assert.True(source.IsSongChecked(song));

        var blockedPayload = source.CreateDragPayload(song);
        Assert.Empty(blockedPayload.Songs);
        Assert.False(source.AddToOppositeCommand.CanExecute(null));
        Assert.False(target.DropCommand.CanExecute(blockedPayload));
        Assert.False(vm.DropSongCommand.CanExecute(song));
        vm.AddToOpposite(source, [song]);
        vm.DropOnPane(target, blockedPayload);
        Assert.Equal(["Test Playlist B"], song.PlaylistNames);

        source.RemoveBlacklistCommand.Execute(null);
        Assert.False(song.IsBlacklisted);
        Assert.Equal(["Test Playlist B"], song.PlaylistNames);
        source.SelectedCollection = vm.SmartNavigation.Single(item => item.Filter == NavigationFilter.AllSongs);
        var allowedPayload = source.CreateDragPayload(song);
        Assert.Single(allowedPayload.Songs);
        Assert.True(target.DropCommand.CanExecute(allowedPayload));
        target.DropCommand.Execute(allowedPayload);
        Assert.Contains("Test Playlist A", song.PlaylistNames);
        Assert.Contains("Test Playlist B", song.PlaylistNames);
    }

    public void Dispose() => Directory.Delete(_root, true);
}
