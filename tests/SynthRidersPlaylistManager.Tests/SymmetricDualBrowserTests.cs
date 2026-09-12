using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Tests;

public sealed class SymmetricDualBrowserTests
{
    [Fact]
    public void PaneAToPaneBDropAddsToPaneBPlaylist()
    {
        var vm = CreateAllSongsTo("Test Playlist A"); var song = vm.PaneA.VisibleSongs.Cast<SongItemViewModel>().First(x => !x.PlaylistNames.Contains("Test Playlist A"));
        vm.PaneB.DropCommand.Execute(vm.PaneA.CreateDragPayload(song));
        Assert.Contains("Test Playlist A", song.PlaylistNames);
    }

    [Fact]
    public void PaneBToPaneADropAddsToPaneAPlaylist()
    {
        var vm = ProductionViewModelFixture.Create(); vm.PaneA.SelectedCollection = Playlist(vm, "Test Playlist B"); vm.PaneB.SelectedCollection = Playlist(vm, "Test Playlist A");
        var song = vm.PaneB.VisibleSongs.Cast<SongItemViewModel>().First(x => !x.PlaylistNames.Contains("Test Playlist B"));
        vm.PaneA.DropCommand.Execute(vm.PaneB.CreateDragPayload(song));
        Assert.Contains("Test Playlist B", song.PlaylistNames);
    }

    [Fact]
    public void PaneAMultiDragUsesOnlyPaneAChecks()
    {
        var vm = CreateAllSongsTo("Test Playlist A"); var songs = vm.PaneA.VisibleSongs.Cast<SongItemViewModel>().Where(x => !x.PlaylistNames.Contains("Test Playlist A")).Take(2).ToArray();
        foreach (var song in songs) song.IsSourceChecked = true; vm.Songs.Last().IsDestinationChecked = true;
        var payload = vm.PaneA.CreateDragPayload(songs[0]);
        Assert.Equal(2, payload.Count); Assert.DoesNotContain(vm.Songs.Last(), payload.Songs);
    }

    [Fact]
    public void PaneBMultiDragUsesOnlyPaneBChecks()
    {
        var vm = ProductionViewModelFixture.Create(); vm.PaneB.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs);
        vm.Songs[2].IsDestinationChecked = true; vm.Songs[3].IsDestinationChecked = true; vm.Songs[4].IsSourceChecked = true;
        var payload = vm.PaneB.CreateDragPayload(vm.Songs[2]);
        Assert.Equal(2, payload.Count); Assert.DoesNotContain(vm.Songs[4], payload.Songs);
    }

    [Fact]
    public void PaneSelectionsAreIndependent()
    {
        var vm = ProductionViewModelFixture.Create(); vm.Songs[2].IsSourceChecked = true; vm.Songs[3].IsDestinationChecked = true;
        Assert.Equal(1, vm.PaneA.CheckedCount); Assert.Equal(1, vm.PaneB.CheckedCount);
        vm.PaneA.ClearSelectionCommand.Execute(null); Assert.Equal(0, vm.PaneA.CheckedCount); Assert.Equal(1, vm.PaneB.CheckedCount);
    }

    [Fact]
    public void PaneSearchesAreIndependent()
    {
        var vm = ProductionViewModelFixture.Create(); var all = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs); vm.PaneA.SelectedCollection = all; vm.PaneB.SelectedCollection = all;
        vm.PaneA.SearchText = "Search Mapper"; vm.PaneB.SearchText = "Test Song Alpha";
        Assert.Single(vm.PaneA.VisibleSongs.Cast<SongItemViewModel>()); Assert.Equal("Test Song Beta", vm.PaneA.VisibleSongs.Cast<SongItemViewModel>().Single().Title);
        Assert.Single(vm.PaneB.VisibleSongs.Cast<SongItemViewModel>()); Assert.Equal("Test Song Alpha", vm.PaneB.VisibleSongs.Cast<SongItemViewModel>().Single().Title);
    }

    [Fact]
    public void SameCollectionDisablesBothDirections()
    {
        var vm = ProductionViewModelFixture.Create(); var playlist = Playlist(vm, "Test Playlist A"); vm.PaneA.SelectedCollection = playlist; vm.PaneB.SelectedCollection = playlist;
        Assert.False(vm.CanAddToOpposite(vm.PaneA)); Assert.False(vm.CanAddToOpposite(vm.PaneB));
        Assert.False(vm.PaneA.DropCommand.CanExecute(new SongDragPayload([vm.Songs[0]], "B"))); Assert.False(vm.PaneB.DropCommand.CanExecute(new SongDragPayload([vm.Songs[0]], "A")));
    }

    [Fact]
    public void NonPlaylistCollectionsRejectDrop()
    {
        var vm = ProductionViewModelFixture.Create(); vm.PaneA.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs); vm.PaneB.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.Custom);
        Assert.False(vm.CanReceiveDrop(vm.PaneA)); Assert.False(vm.CanReceiveDrop(vm.PaneB));
    }

    [Fact]
    public void GameRunningBlocksBidirectionalEditing()
    {
        var vm = ProductionViewModelFixture.Create(); vm.PaneA.SelectedCollection = Playlist(vm, "Test Playlist B"); vm.PaneB.SelectedCollection = Playlist(vm, "Test Playlist A"); vm.GameState = GameAccessState.RunningReadOnly;
        Assert.False(vm.CanAddToOpposite(vm.PaneA)); Assert.False(vm.CanAddToOpposite(vm.PaneB));
        Assert.False(vm.PaneA.DropCommand.CanExecute(new SongDragPayload([vm.Songs[0]], "B"))); Assert.False(vm.PaneB.DropCommand.CanExecute(new SongDragPayload([vm.Songs[0]], "A")));
    }

    [Fact]
    public void ChangingPaneACollectionDoesNotChangePaneB()
    {
        var vm = ProductionViewModelFixture.Create(); var paneB = vm.PaneB.SelectedCollection;
        vm.PaneA.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.Custom);
        Assert.Same(paneB, vm.PaneB.SelectedCollection);
    }

    [Fact]
    public void ChangingPaneBCollectionDoesNotChangePaneA()
    {
        var vm = ProductionViewModelFixture.Create(); var paneA = vm.PaneA.SelectedCollection;
        vm.PaneB.SelectedCollection = vm.PlaylistNavigation.First(x => x.PlaylistName == "Test Playlist B");
        Assert.Same(paneA, vm.PaneA.SelectedCollection);
    }

    [Fact]
    public void RowSelectionFromEitherPaneUsesSharedMiniPlayerSelection()
    {
        var vm = ProductionViewModelFixture.Create(); var fromA = vm.PaneA.VisibleSongs.Cast<SongItemViewModel>().First(); vm.SelectedSong = fromA; Assert.Same(fromA, vm.SelectedSong);
        var fromB = vm.PaneB.VisibleSongs.Cast<SongItemViewModel>().Last(); vm.SelectedSong = fromB; Assert.Same(fromB, vm.SelectedSong);
    }

    [Fact]
    public void CheckboxChangesDoNotReplaceMiniPlayerSelection()
    {
        var vm = ProductionViewModelFixture.Create(); var selected = vm.SelectedSong; vm.Songs.Last().IsSourceChecked = true; vm.Songs.First().IsDestinationChecked = true;
        Assert.Same(selected, vm.SelectedSong);
    }

    [Fact]
    public void ActivatingOtherPanePreservesBothPaneCheckStates()
    {
        var vm = ProductionViewModelFixture.Create();
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
        var vm = ProductionViewModelFixture.Create();
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
        var vm = ProductionViewModelFixture.Create(); vm.PaneA.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs); vm.PaneB.SelectedCollection = Playlist(vm, playlistName); return vm;
    }
    private static NavigationItemViewModel Playlist(MainViewModel vm, string name) => vm.PlaylistNavigation.First(x => x.PlaylistName == name);
}
