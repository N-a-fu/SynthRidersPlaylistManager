using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void FavoriteGlyphUsesCompleteHeartStates()
    {
        var viewModel = new MainViewModel();
        var song = viewModel.Songs[0];

        song.IsFavorite = true;
        Assert.Equal("♥", song.FavoriteGlyph);
        song.IsFavorite = false;
        Assert.Equal("♡", song.FavoriteGlyph);
    }

    [Fact]
    public void CreatesWithSelectedFavoriteAndMockCounts()
    {
        var viewModel = new MainViewModel();

        Assert.Equal(30, viewModel.LibraryCount);
        Assert.Equal(4, viewModel.PlaylistCount);
        Assert.NotNull(viewModel.SelectedSong);
        Assert.True(viewModel.SelectedSong.IsFavorite);
        Assert.Equal(NavigationFilter.UnsortedFavorites, viewModel.SelectedNavigation?.Filter);
    }

    [Fact]
    public void SearchFiltersTitleArtistAndMapper()
    {
        var viewModel = new MainViewModel
        {
            SelectedNavigation = new NavigationItemViewModel("すべての曲", NavigationFilter.AllSongs),
            SearchText = "Test Mapper"
        };

        var results = viewModel.SongsView.Cast<SongItemViewModel>().ToArray();

        Assert.Single(results);
        Assert.Equal("TEST CUSTOM SONG", results[0].Title);
    }

    [Fact]
    public void PlaylistNavigationShowsSpmTestSongsInStoredOrder()
    {
        var viewModel = new MainViewModel();
        viewModel.SelectedNavigation = Assert.Single(viewModel.PlaylistNavigation, item => item.PlaylistName == "TEST_PLAYLIST");

        var results = viewModel.SongsView.Cast<SongItemViewModel>().ToArray();

        Assert.Collection(results,
            first => Assert.Equal("TEST OFFICIAL SONG", first.Title),
            second => Assert.Equal("TEST CUSTOM SONG", second.Title));
    }

    [Fact]
    public void FavoriteCommandChangesMockStateOnly()
    {
        var viewModel = new MainViewModel
        {
            SelectedNavigation = new NavigationItemViewModel("すべての曲", NavigationFilter.AllSongs)
        };
        var song = viewModel.Songs.First(item => !item.IsFavorite);
        var before = viewModel.FavoriteCount;

        viewModel.ToggleFavoriteCommand.Execute(song);

        Assert.True(song.IsFavorite);
        Assert.Equal(before + 1, viewModel.FavoriteCount);
    }

    [Fact]
    public void SelectingSongUpdatesDetailSelection()
    {
        var viewModel = new MainViewModel();
        var expected = viewModel.Songs.Last();

        viewModel.SelectedSong = expected;

        Assert.Same(expected, viewModel.SelectedSong);
        Assert.True(viewModel.HasSelectedSong);
    }

    [Fact]
    public void DestinationChangesWithoutChangingSource()
    {
        var viewModel = new MainViewModel();
        var source = viewModel.SelectedSourceNavigation;
        var workout = Assert.Single(viewModel.PlaylistNavigation, item => item.PlaylistName == "Workout");

        viewModel.SelectedDestinationNavigation = workout;

        Assert.Same(source, viewModel.SelectedSourceNavigation);
        Assert.Equal("Workout", viewModel.DestinationPlaylistName);
        Assert.Equal(viewModel.Songs.Count(song => song.PlaylistNames.Contains("Workout")), viewModel.DestinationSongCount);
    }

    [Fact]
    public void DropAddsMockMembershipAndPreventsDuplicate()
    {
        var viewModel = new MainViewModel();
        var song = viewModel.SourceSongsView.Cast<SongItemViewModel>().First(item => !item.PlaylistNames.Contains("TEST_PLAYLIST"));
        var before = viewModel.DestinationSongCount;

        viewModel.DropSongCommand.Execute(song);
        viewModel.DropSongCommand.Execute(song);

        Assert.Equal(before + 1, viewModel.DestinationSongCount);
        Assert.Single(song.PlaylistNames, name => name == "TEST_PLAYLIST");
        Assert.True(viewModel.HasMockChanges);
        Assert.Contains("1曲は既に登録済み", viewModel.StatusMessage);
    }

    [Fact]
    public void UnsortedFavoritesContainOnlyFavoriteSongsWithoutMemberships()
    {
        var viewModel = new MainViewModel
        {
            SelectedSourceNavigation = new NavigationItemViewModel("未整理", NavigationFilter.UnsortedFavorites)
        };

        var results = viewModel.SourceSongsView.Cast<SongItemViewModel>().ToArray();

        Assert.NotEmpty(results);
        Assert.All(results, song =>
        {
            Assert.True(song.IsFavorite);
            Assert.Empty(song.PlaylistNames);
        });
    }

    [Fact]
    public void MultiplePlaylistMembershipRemainsACollection()
    {
        var viewModel = new MainViewModel();
        var song = viewModel.Songs.First(item => item.PlaylistNames.Count > 1);

        Assert.True(song.PlaylistNames.Count > 1);
        Assert.Contains("TEST_PLAYLIST", viewModel.Songs.First(item => item.Title == "TEST CUSTOM SONG").PlaylistNames);
    }

    [Fact]
    public void GameRunningDisablesMockEditingCommands()
    {
        var viewModel = new MainViewModel
        {
            GameState = GameAccessState.RunningReadOnly
        };
        var song = viewModel.Songs[0];

        Assert.False(viewModel.DropSongCommand.CanExecute(song));
        Assert.False(viewModel.ToggleFavoriteCommand.CanExecute(song));
        Assert.True(viewModel.IsReadOnly);
    }

    [Fact]
    public void CheckboxSelectionIsIndependentForSourceAndDestination()
    {
        var vm = new MainViewModel(); var song = vm.DestinationSongs[0];
        song.IsSourceChecked = true;
        Assert.Equal(1, vm.SourceSelectedCount); Assert.Equal(0, vm.DestinationSelectedCount);
        song.IsDestinationChecked = true;
        Assert.Equal(1, vm.SourceSelectedCount); Assert.Equal(1, vm.DestinationSelectedCount);
    }

    [Fact]
    public void SelectAllTargetsOnlyVisibleAndClearRemovesHiddenSelections()
    {
        var vm = new MainViewModel { SelectedSourceNavigation = new("すべて", NavigationFilter.AllSongs), SearchText = "Test Mapper" };
        vm.SelectAllVisibleSourceCommand.Execute(null);
        Assert.Equal(1, vm.SourceSelectedCount); Assert.Equal(1, vm.VisibleSourceSelectedCount);
        vm.SearchText = "";
        Assert.Equal(1, vm.SourceSelectedCount); Assert.Equal(1, vm.VisibleSourceSelectedCount);
        vm.ClearSourceSelectionCommand.Execute(null); Assert.Equal(0, vm.SourceSelectedCount);
    }

    [Fact]
    public void CheckedSongDragBuildsMultiPayloadButUncheckedDragIsSingle()
    {
        var vm = new MainViewModel { SelectedSourceNavigation = new("すべて", NavigationFilter.AllSongs) };
        vm.Songs[2].IsSourceChecked = true; vm.Songs[3].IsSourceChecked = true;
        var multiple = vm.CreateSourceDragPayload(vm.Songs[2]); var single = vm.CreateSourceDragPayload(vm.Songs[4]);
        Assert.Equal(2, multiple.Count); Assert.Single(single.Songs); Assert.Same(vm.Songs[4], single.Songs[0]);
        Assert.True(vm.Songs[2].IsSourceChecked); Assert.True(vm.Songs[3].IsSourceChecked);
    }

    [Fact]
    public void BulkAddAddsUniqueSongsAndReportsDuplicates()
    {
        var vm = new MainViewModel { SelectedSourceNavigation = new("すべて", NavigationFilter.AllSongs) };
        var duplicate = vm.Songs.First(x => x.PlaylistNames.Contains("TEST_PLAYLIST")); var newSong = vm.Songs.First(x => !x.PlaylistNames.Contains("TEST_PLAYLIST"));
        duplicate.IsSourceChecked = true; newSong.IsSourceChecked = true; var before = vm.DestinationSongCount;
        vm.BulkAddCommand.Execute(null);
        Assert.Equal(before + 1, vm.DestinationSongCount); Assert.Contains("1曲は既に登録済み", vm.StatusMessage); Assert.True(vm.HasMockChanges);
    }

    [Fact]
    public void BulkMoveRemovesRealSourceMembershipAndAddsDestination()
    {
        var vm = new MainViewModel(); var source = vm.PlaylistNavigation.First(x => x.PlaylistName == "Workout"); vm.SelectedSourceNavigation = source;
        var song = vm.SourceSongsView.Cast<SongItemViewModel>().First(x => !x.PlaylistNames.Contains("TEST_PLAYLIST")); song.IsSourceChecked = true;
        Assert.True(vm.BulkMoveCommand.CanExecute(null)); vm.BulkMoveCommand.Execute(null);
        Assert.DoesNotContain("Workout", song.PlaylistNames); Assert.Contains("TEST_PLAYLIST", song.PlaylistNames);
    }

    [Fact]
    public void SameSourceAndDestinationDisablesAddAndMove()
    {
        var vm = new MainViewModel(); var playlist = vm.PlaylistNavigation.First(x => x.PlaylistName == "TEST_PLAYLIST"); vm.SelectedSourceNavigation = playlist;
        vm.SourceSongsView.Cast<SongItemViewModel>().First().IsSourceChecked = true;
        Assert.True(vm.IsSamePlaylist); Assert.False(vm.BulkAddCommand.CanExecute(null)); Assert.False(vm.BulkMoveCommand.CanExecute(null));
    }

    [Fact]
    public void PlaylistCreateRenameDuplicateAndDeleteAreMockOperations()
    {
        var vm = new MainViewModel(); var start = vm.PlaylistCount; vm.PlaylistNameDraft = "New Mix"; vm.CreatePlaylistCommand.Execute(null);
        Assert.Equal(start + 1, vm.PlaylistCount); Assert.Equal("New Mix", vm.DestinationPlaylistName);
        vm.PlaylistNameDraft = "Renamed Mix"; vm.RenamePlaylistCommand.Execute(null); Assert.Equal("Renamed Mix", vm.DestinationPlaylistName);
        var renamed = vm.SelectedDestinationNavigation!; vm.DuplicatePlaylistCommand.Execute(renamed); Assert.Equal(start + 2, vm.PlaylistCount); Assert.StartsWith("Renamed Mix Copy", vm.DestinationPlaylistName);
        var copy = vm.SelectedDestinationNavigation!; vm.RequestDeletePlaylistCommand.Execute(copy); Assert.True(vm.IsDeleteConfirmationOpen); vm.ConfirmDeletePlaylistCommand.Execute(null);
        Assert.Equal(start + 1, vm.PlaylistCount); Assert.False(vm.IsDeleteConfirmationOpen); Assert.True(vm.HasMockChanges);
    }

    [Fact]
    public void DuplicateCopiesMembersAndAvoidsNameCollision()
    {
        var vm = new MainViewModel(); var source = vm.PlaylistNavigation.First(x => x.PlaylistName == "TEST_PLAYLIST");
        vm.DuplicatePlaylistCommand.Execute(source); var first = vm.DestinationPlaylistName; vm.DuplicatePlaylistCommand.Execute(source); var second = vm.DestinationPlaylistName;
        Assert.NotEqual(first, second); Assert.Equal(2, vm.Songs.Count(x => x.PlaylistNames.Contains(first))); Assert.Equal(2, vm.Songs.Count(x => x.PlaylistNames.Contains(second)));
    }

    [Fact]
    public void BulkFavoriteOnAndOffAreIndependentFromPlaylistAdd()
    {
        var vm = new MainViewModel { SelectedSourceNavigation = new("すべて", NavigationFilter.AllSongs) }; var song = vm.Songs.First(x => !x.IsFavorite); song.IsSourceChecked = true;
        vm.FavoriteBulkOnCommand.Execute(null); Assert.True(song.IsFavorite); vm.FavoriteBulkOffCommand.Execute(null); Assert.False(song.IsFavorite); Assert.DoesNotContain("TEST_PLAYLIST", song.PlaylistNames);
    }

    [Fact]
    public void GameRunningDisablesEveryEditingCommandButKeepsChecksAvailable()
    {
        var vm = new MainViewModel { SelectedSourceNavigation = new("すべて", NavigationFilter.AllSongs), GameState = GameAccessState.RunningReadOnly }; var song = vm.Songs[2]; song.IsSourceChecked = true; vm.PlaylistNameDraft = "Blocked";
        Assert.True(song.IsSourceChecked); Assert.False(vm.BulkAddCommand.CanExecute(null)); Assert.False(vm.BulkMoveCommand.CanExecute(null)); Assert.False(vm.FavoriteBulkOnCommand.CanExecute(null)); Assert.False(vm.CreatePlaylistCommand.CanExecute(null));
        var playlist = vm.PlaylistNavigation[0]; Assert.False(vm.DuplicatePlaylistCommand.CanExecute(playlist)); Assert.False(vm.RequestDeletePlaylistCommand.CanExecute(playlist));
    }
}
