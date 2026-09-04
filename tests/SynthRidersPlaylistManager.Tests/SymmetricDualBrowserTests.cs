using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Tests;

public sealed class SymmetricDualBrowserTests
{
    [Fact]
    public void PaneAToPaneBDropAddsToPaneBPlaylist()
    {
        var vm = CreateAllSongsTo("TEST_PLAYLIST"); var song = vm.PaneA.VisibleSongs.Cast<SongItemViewModel>().First(x => !x.PlaylistNames.Contains("TEST_PLAYLIST"));
        vm.PaneB.DropCommand.Execute(vm.PaneA.CreateDragPayload(song));
        Assert.Contains("TEST_PLAYLIST", song.PlaylistNames); Assert.True(vm.HasMockChanges);
    }

    [Fact]
    public void PaneBToPaneADropAddsToPaneAPlaylist()
    {
        var vm = new MainViewModel(); vm.PaneA.SelectedCollection = Playlist(vm, "Workout"); vm.PaneB.SelectedCollection = Playlist(vm, "TEST_PLAYLIST");
        var song = vm.PaneB.VisibleSongs.Cast<SongItemViewModel>().First(x => !x.PlaylistNames.Contains("Workout"));
        vm.PaneA.DropCommand.Execute(vm.PaneB.CreateDragPayload(song));
        Assert.Contains("Workout", song.PlaylistNames);
    }

    [Fact]
    public void PaneAMultiDragUsesOnlyPaneAChecks()
    {
        var vm = CreateAllSongsTo("TEST_PLAYLIST"); var songs = vm.PaneA.VisibleSongs.Cast<SongItemViewModel>().Where(x => !x.PlaylistNames.Contains("TEST_PLAYLIST")).Take(2).ToArray();
        foreach (var song in songs) song.IsSourceChecked = true; vm.Songs[0].IsDestinationChecked = true;
        var payload = vm.PaneA.CreateDragPayload(songs[0]);
        Assert.Equal(2, payload.Count); Assert.DoesNotContain(vm.Songs[0], payload.Songs);
    }

    [Fact]
    public void PaneBMultiDragUsesOnlyPaneBChecks()
    {
        var vm = new MainViewModel(); vm.PaneB.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs);
        vm.Songs[2].IsDestinationChecked = true; vm.Songs[3].IsDestinationChecked = true; vm.Songs[4].IsSourceChecked = true;
        var payload = vm.PaneB.CreateDragPayload(vm.Songs[2]);
        Assert.Equal(2, payload.Count); Assert.DoesNotContain(vm.Songs[4], payload.Songs);
    }

    [Fact]
    public void PaneSelectionsAreIndependent()
    {
        var vm = new MainViewModel(); vm.Songs[2].IsSourceChecked = true; vm.Songs[3].IsDestinationChecked = true;
        Assert.Equal(1, vm.PaneA.CheckedCount); Assert.Equal(1, vm.PaneB.CheckedCount);
        vm.PaneA.ClearSelectionCommand.Execute(null); Assert.Equal(0, vm.PaneA.CheckedCount); Assert.Equal(1, vm.PaneB.CheckedCount);
    }

    [Fact]
    public void PaneSearchesAreIndependent()
    {
        var vm = new MainViewModel(); var all = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs); vm.PaneA.SelectedCollection = all; vm.PaneB.SelectedCollection = all;
        vm.PaneA.SearchText = "Test Mapper"; vm.PaneB.SearchText = "PiSk";
        Assert.Single(vm.PaneA.VisibleSongs.Cast<SongItemViewModel>()); Assert.Equal("TEST CUSTOM SONG", vm.PaneA.VisibleSongs.Cast<SongItemViewModel>().Single().Title);
        Assert.Single(vm.PaneB.VisibleSongs.Cast<SongItemViewModel>()); Assert.Equal("TEST OFFICIAL SONG", vm.PaneB.VisibleSongs.Cast<SongItemViewModel>().Single().Title);
    }

    [Fact]
    public void SameCollectionDisablesBothDirections()
    {
        var vm = new MainViewModel(); var playlist = Playlist(vm, "TEST_PLAYLIST"); vm.PaneA.SelectedCollection = playlist; vm.PaneB.SelectedCollection = playlist;
        Assert.False(vm.CanAddToOpposite(vm.PaneA)); Assert.False(vm.CanAddToOpposite(vm.PaneB));
        Assert.False(vm.PaneA.DropCommand.CanExecute(new SongDragPayload([vm.Songs[0]], "B"))); Assert.False(vm.PaneB.DropCommand.CanExecute(new SongDragPayload([vm.Songs[0]], "A")));
    }

    [Fact]
    public void NonPlaylistCollectionsRejectDrop()
    {
        var vm = new MainViewModel(); vm.PaneA.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs); vm.PaneB.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.Custom);
        Assert.False(vm.CanReceiveDrop(vm.PaneA)); Assert.False(vm.CanReceiveDrop(vm.PaneB));
    }

    [Fact]
    public void PlaylistMoveWorksInBothDirections()
    {
        var vm = new MainViewModel(); vm.PaneA.SelectedCollection = Playlist(vm, "Workout"); vm.PaneB.SelectedCollection = Playlist(vm, "TEST_PLAYLIST");
        var aSong = vm.PaneA.VisibleSongs.Cast<SongItemViewModel>().First(x => !x.PlaylistNames.Contains("TEST_PLAYLIST")); vm.MoveToOpposite(vm.PaneA, [aSong]);
        Assert.DoesNotContain("Workout", aSong.PlaylistNames); Assert.Contains("TEST_PLAYLIST", aSong.PlaylistNames);
        var bSong = vm.PaneB.VisibleSongs.Cast<SongItemViewModel>().First(x => !x.PlaylistNames.Contains("Workout")); vm.MoveToOpposite(vm.PaneB, [bSong]);
        Assert.DoesNotContain("TEST_PLAYLIST", bSong.PlaylistNames); Assert.Contains("Workout", bSong.PlaylistNames);
    }

    [Fact]
    public void GameRunningBlocksBidirectionalEditing()
    {
        var vm = new MainViewModel(); vm.PaneA.SelectedCollection = Playlist(vm, "Workout"); vm.PaneB.SelectedCollection = Playlist(vm, "TEST_PLAYLIST"); vm.GameState = GameAccessState.RunningReadOnly;
        Assert.False(vm.CanAddToOpposite(vm.PaneA)); Assert.False(vm.CanAddToOpposite(vm.PaneB)); Assert.False(vm.CanMoveToOpposite(vm.PaneA)); Assert.False(vm.CanMoveToOpposite(vm.PaneB));
        Assert.False(vm.PaneA.DropCommand.CanExecute(new SongDragPayload([vm.Songs[0]], "B"))); Assert.False(vm.PaneB.DropCommand.CanExecute(new SongDragPayload([vm.Songs[0]], "A")));
    }

    [Fact]
    public void ChangingPaneACollectionDoesNotChangePaneB()
    {
        var vm = new MainViewModel(); var paneB = vm.PaneB.SelectedCollection;
        vm.PaneA.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.Custom);
        Assert.Same(paneB, vm.PaneB.SelectedCollection);
    }

    [Fact]
    public void ChangingPaneBCollectionDoesNotChangePaneA()
    {
        var vm = new MainViewModel(); var paneA = vm.PaneA.SelectedCollection;
        vm.PaneB.SelectedCollection = vm.PlaylistNavigation.First(x => x.PlaylistName == "Workout");
        Assert.Same(paneA, vm.PaneA.SelectedCollection);
    }

    [Fact]
    public void RowSelectionFromEitherPaneUsesSharedMiniPlayerSelection()
    {
        var vm = new MainViewModel(); var fromA = vm.PaneA.VisibleSongs.Cast<SongItemViewModel>().First(); vm.SelectedSong = fromA; Assert.Same(fromA, vm.SelectedSong);
        var fromB = vm.PaneB.VisibleSongs.Cast<SongItemViewModel>().Last(); vm.SelectedSong = fromB; Assert.Same(fromB, vm.SelectedSong);
    }

    [Fact]
    public void CheckboxChangesDoNotReplaceMiniPlayerSelection()
    {
        var vm = new MainViewModel(); var selected = vm.SelectedSong; vm.Songs.Last().IsSourceChecked = true; vm.Songs.First().IsDestinationChecked = true;
        Assert.Same(selected, vm.SelectedSong);
    }

    [Fact]
    public void ActivatingOtherPanePreservesBothPaneCheckStates()
    {
        var vm = new MainViewModel();
        vm.Songs[2].IsSourceChecked = true;
        vm.Songs[3].IsDestinationChecked = true;

        vm.PaneB.Activate();

        Assert.False(vm.PaneA.IsActive);
        Assert.True(vm.PaneB.IsActive);
        Assert.True(vm.Songs[2].IsSourceChecked);
        Assert.True(vm.Songs[3].IsDestinationChecked);

        vm.PaneA.Activate();

        Assert.True(vm.PaneA.IsActive);
        Assert.False(vm.PaneB.IsActive);
        Assert.True(vm.Songs[2].IsSourceChecked);
        Assert.True(vm.Songs[3].IsDestinationChecked);
    }

    [Fact]
    public void FavoriteToggleDoesNotChangeRowOrCheckboxSelectionModel()
    {
        var vm = new MainViewModel();
        var selected = vm.SelectedSong;
        var song = vm.Songs.Last();
        song.IsSourceChecked = true;
        var wasFavorite = song.IsFavorite;

        vm.ToggleFavoriteCommand.Execute(song);

        Assert.Equal(!wasFavorite, song.IsFavorite);
        Assert.True(song.IsSourceChecked);
        Assert.Same(selected, vm.SelectedSong);
    }

    private static MainViewModel CreateAllSongsTo(string playlistName)
    {
        var vm = new MainViewModel(); vm.PaneA.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs); vm.PaneB.SelectedCollection = Playlist(vm, playlistName); return vm;
    }
    private static NavigationItemViewModel Playlist(MainViewModel vm, string name) => vm.PlaylistNavigation.First(x => x.PlaylistName == name);
}
