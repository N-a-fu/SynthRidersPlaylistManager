using System.IO;
using System.Text.Json.Nodes;
using SynthRidersPlaylistManager.App.Services;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Core.Services;
using SynthRidersPlaylistManager.Infrastructure.GameData;

namespace SynthRidersPlaylistManager.Tests;

public sealed class FavoriteFlagsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "srpm-favorite-" + Guid.NewGuid().ToString("N"));
    private string PathName => Path.Combine(_root, "favorites.bin");
    private static string Hash => new('a', 64);
    private static string Entry => "Track-Artist-" + Hash;
    public FavoriteFlagsTests() { Directory.CreateDirectory(_root); File.WriteAllText(PathName, "{\"Favorite\":[],\"unknown\":{\"n\":42,\"a\":[null,true,\"保持\"]}}"); }

    [Fact]
    public void OnOffPreservesUnknownFieldsAndBacksUpOriginal()
    {
        var store = new SongFlagsStore(() => true);
        var before = File.ReadAllBytes(PathName);
        Assert.Contains(Entry, store.SetFavorites(PathName, [Entry], true));
        Assert.Equal(before, File.ReadAllBytes(Assert.Single(Directory.GetFiles(_root, "*.bak"))));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(before)!["unknown"], JsonNode.Parse(File.ReadAllText(PathName))!["unknown"]));
        var written = File.ReadAllBytes(PathName);
        store.SetFavorites(PathName, [Entry, "Renamed-Artist-" + Hash], true);
        Assert.Equal(written, File.ReadAllBytes(PathName));
        store.SetFavorites(PathName, ["Other-Artist-" + new string('b', 64)], false);
        Assert.Equal(written, File.ReadAllBytes(PathName));
        Assert.Empty(store.SetFavorites(PathName, ["Renamed-Artist-" + Hash], false));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void ExistingUnknownRecordsArePreservedAndTargetDuplicatesAreRemoved()
    {
        File.WriteAllText(PathName, new JsonObject { ["Favorite"] = new JsonArray(Entry, "Old-Artist-" + Hash.ToUpperInvariant(), "unknown-record") }.ToJsonString());
        var store = new SongFlagsStore(() => true);
        var result = store.SetFavorites(PathName, [Entry], true);
        Assert.Equal(2, result.Count);
        Assert.Contains("unknown-record", result);
        Assert.Equal("unknown-record", Assert.Single(store.SetFavorites(PathName, [Entry], false)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void GameRunningOrStartingBeforeReplaceKeepsOriginal(int stopAt)
    {
        var before = File.ReadAllBytes(PathName);
        var checks = 0;
        Assert.Throws<InvalidOperationException>(() => new SongFlagsStore(() => ++checks < stopAt).SetFavorites(PathName, [Entry], true));
        Assert.Equal(before, File.ReadAllBytes(PathName));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void FailedReplaceKeepsOriginal()
    {
        var before = File.ReadAllBytes(PathName);
        using var locked = new FileStream(PathName, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Throws<IOException>(() => new SongFlagsStore(() => true).SetFavorites(PathName, [Entry], true));
        Assert.Equal(before, File.ReadAllBytes(PathName));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Favorite\":[null]}")]
    public void InvalidFavoriteArrayIsNotReinitialized(string json)
    {
        File.WriteAllText(PathName, json);
        Assert.Throws<InvalidDataException>(() => new SongFlagsStore(() => true).SetFavorites(PathName, [Entry], true));
        Assert.Equal(json, File.ReadAllText(PathName));
        Assert.Empty(Directory.GetFiles(_root, "*.bak"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RealHeartWritesAndRefreshesBothFavoritesViewsWithoutChangingSelection(bool fromA)
    {
        var env = new EnvironmentDiscoveryResult([new(DataLocationKind.Favorites, PathName, DataLocationSource.Derived, DataLocationStatus.Available, DateTimeOffset.Now, "fixture", false)], "fixture", DateTimeOffset.Now);
        Song[] songs = [new(new(SongKind.Custom, Hash), "Track", "Artist", "Mapper", null, null, "", false, [], null, FileName: "track.synth", FavoriteReference: Entry),
            new(new(SongKind.Custom, new string('b', 64)), "Other", "Artist", "Mapper", null, null, "", false, [], null)];
        var snapshot = new LibrarySnapshot(songs, [], [], [], new(2, 2, 0, 0, 0, 0, 0, 0, 0));
        using var vm = new MainViewModel(new MockLibraryDataSource(), new EnvironmentStub(env), null, new ReaderStub(snapshot), songFlagsStore: new SongFlagsStore(() => true));
        await vm.InitializeEnvironmentAsync();
        var target = vm.Songs[0]; var other = vm.Songs[1];
        vm.PaneA.SelectedCollection = vm.FavoritesNavigation[0];
        vm.PaneB.SelectedCollection = vm.FavoritesNavigation[1];
        vm.PaneA.SelectedSong = other; vm.PaneB.SelectedSong = other;
        target.IsSourceChecked = true;
        var pane = fromA ? vm.PaneA : vm.PaneB;
        Assert.True(pane.ToggleFavoriteCommand.CanExecute(target));
        pane.ToggleFavoriteCommand.Execute(target);
        Assert.True(target.IsFavorite);
        Assert.Contains(Entry, File.ReadAllText(PathName));
        Assert.Same(target, Assert.Single(vm.PaneA.VisibleSongs.Cast<SongItemViewModel>()));
        Assert.Same(target, Assert.Single(vm.PaneB.VisibleSongs.Cast<SongItemViewModel>()));
        pane.ToggleFavoriteCommand.Execute(target);
        Assert.False(target.IsFavorite);
        Assert.DoesNotContain(Entry, File.ReadAllText(PathName));
        Assert.Empty(vm.PaneA.VisibleSongs.Cast<object>()); Assert.Empty(vm.PaneB.VisibleSongs.Cast<object>());
        Assert.Same(other, vm.SelectedSong); Assert.Same(other, vm.PaneA.SelectedSong); Assert.Same(other, vm.PaneB.SelectedSong);
        Assert.True(target.IsSourceChecked); Assert.False(target.IsDestinationChecked);
        vm.GameState = GameAccessState.RunningReadOnly;
        Assert.False(pane.ToggleFavoriteCommand.CanExecute(target));
        pane.ToggleFavoriteCommand.Execute(target); Assert.False(target.IsFavorite);
    }

    [Fact]
    public void EveryNormalCollectionOnlyAddsEvenWithMixedOrAlreadyRegisteredChecks()
    {
        using var vm = ProductionViewModelFixture.Create();
        var songs = vm.Songs.Where(s => s.Identity.Kind == SongKind.Custom).Take(2).ToArray();
        foreach (var s in songs) vm.PaneA.ToggleCheckedCommand.Execute(s);
        foreach (var collection in vm.AllCollections.Where(c => c.Filter != NavigationFilter.Blacklist))
        {
            vm.PaneA.SelectedCollection = collection;
            songs[0].IsBlacklisted = true; songs[1].IsBlacklisted = false;
            Assert.True(vm.PaneA.AddBlacklistCommand.CanExecute(null));
            vm.PaneA.AddBlacklistCommand.Execute(null);
            vm.PaneA.AddBlacklistCommand.Execute(null);
            Assert.All(songs, s => Assert.True(s.IsBlacklisted));
        }
        vm.PaneA.SelectedCollection = vm.SmartNavigation.Single(c => c.Filter == NavigationFilter.Blacklist);
        Assert.False(vm.PaneA.AddBlacklistCommand.CanExecute(null));
        vm.PaneA.RemoveBlacklistCommand.Execute(null);
        Assert.All(songs, s => Assert.False(s.IsBlacklisted));
    }

    [Fact]
    public void LeftBlacklistRemovalOnlyRemovesCheckedRegisteredSongs()
    {
        using var vm = ProductionViewModelFixture.Create();
        var songs = vm.Songs.Where(s => s.Identity.Kind == SongKind.Custom).Take(3).ToArray();
        vm.PaneA.ToggleCheckedCommand.Execute(songs[0]);
        vm.PaneA.ToggleCheckedCommand.Execute(songs[1]);
        vm.PaneA.AddBlacklistCommand.Execute(null);
        vm.PaneA.ClearSelectionCommand.Execute(null);
        vm.PaneA.ToggleCheckedCommand.Execute(songs[0]);
        vm.PaneA.ToggleCheckedCommand.Execute(songs[2]);
        vm.PaneA.SelectedCollection = vm.SmartNavigation.Single(n => n.Filter == NavigationFilter.Blacklist);
        Assert.True(vm.PaneA.RemoveBlacklistCommand.CanExecute(null));
        vm.PaneA.RemoveBlacklistCommand.Execute(null);
        Assert.False(songs[0].IsBlacklisted);
        Assert.True(songs[1].IsBlacklisted);
        Assert.False(songs[2].IsBlacklisted);
        Assert.True(vm.PaneA.IsSongChecked(songs[0]));
        Assert.True(vm.PaneA.IsSongChecked(songs[2]));
        Assert.False(vm.PaneA.RemoveBlacklistCommand.CanExecute(null));
        vm.PaneA.RemoveBlacklistCommand.Execute(null);
        Assert.False(songs[0].IsBlacklisted);
    }

    private sealed class EnvironmentStub(EnvironmentDiscoveryResult result) : IEnvironmentDiscoveryService
    {
        public Task<EnvironmentDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<EnvironmentDiscoveryResult> SetManualOverrideAsync(DataLocationKind kind, string path, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
    private sealed class ReaderStub(LibrarySnapshot snapshot) : IRealLibraryReader
    {
        public Task<LibrarySnapshot> LoadAsync(EnvironmentDiscoveryResult environment, CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }
    public void Dispose() => Directory.Delete(_root, true);
}
