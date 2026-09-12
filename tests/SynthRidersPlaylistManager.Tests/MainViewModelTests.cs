using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.App.Services;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Core.Services;

namespace SynthRidersPlaylistManager.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void FavoriteGlyphUsesCompleteHeartStates()
    {
        var viewModel = ProductionViewModelFixture.Create();
        var song = viewModel.Songs[0];

        song.IsFavorite = true;
        Assert.Equal("♥", song.FavoriteGlyph);
        song.IsFavorite = false;
        Assert.Equal("♡", song.FavoriteGlyph);
    }

    [Fact]
    public void CreatesWithExplicitFixtureCounts()
    {
        var viewModel = ProductionViewModelFixture.Create();

        Assert.Equal(ProductionViewModelFixture.CreateSnapshot().Songs.Count, viewModel.LibraryCount);
        Assert.Equal(4, viewModel.PlaylistCount);
        Assert.NotNull(viewModel.SelectedSong);
        Assert.True(viewModel.SelectedSong.IsFavorite);
        Assert.Equal(NavigationFilter.AllSongs, viewModel.SelectedNavigation?.Filter);
    }

    [Fact]
    public void SearchFiltersTitleArtistAndMapper()
    {
        var viewModel = ProductionViewModelFixture.Create();
        viewModel.SelectedNavigation = new NavigationItemViewModel("すべての曲", NavigationFilter.AllSongs);
        viewModel.SearchText = "Search Mapper";

        var results = viewModel.SongsView.Cast<SongItemViewModel>().ToArray();

        Assert.Single(results);
        Assert.Equal("Test Song Beta", results[0].Title);
    }

    [Fact]
    public void PlaylistNavigationShowsSpmTestSongsInStoredOrder()
    {
        var viewModel = ProductionViewModelFixture.Create();
        viewModel.SelectedNavigation = Assert.Single(viewModel.PlaylistNavigation, item => item.PlaylistName == "Test Playlist A");

        var results = viewModel.SongsView.Cast<SongItemViewModel>().ToArray();

        Assert.Collection(results,
            first => Assert.Equal("Test Song Alpha", first.Title),
            second => Assert.Equal("Test Song Beta", second.Title));
    }

    [Fact]
    public void FavoriteCommandChangesMockStateOnly()
    {
        var viewModel = ProductionViewModelFixture.Create();
        viewModel.SelectedNavigation = new NavigationItemViewModel("すべての曲", NavigationFilter.AllSongs);
        var song = viewModel.Songs.First(item => !item.IsFavorite);
        var before = viewModel.FavoriteCount;

        viewModel.ToggleFavoriteCommand.Execute(song);

        Assert.True(song.IsFavorite);
        Assert.Equal(before + 1, viewModel.FavoriteCount);
    }

    [Fact]
    public void SelectingSongUpdatesDetailSelection()
    {
        var viewModel = ProductionViewModelFixture.Create();
        var expected = viewModel.Songs.Last();

        viewModel.SelectedSong = expected;

        Assert.Same(expected, viewModel.SelectedSong);
        Assert.True(viewModel.HasSelectedSong);
    }

    [Fact]
    public void DestinationChangesWithoutChangingSource()
    {
        var viewModel = ProductionViewModelFixture.Create();
        var source = viewModel.SelectedSourceNavigation;
        var workout = Assert.Single(viewModel.PlaylistNavigation, item => item.PlaylistName == "Test Playlist B");

        viewModel.SelectedDestinationNavigation = workout;

        Assert.Same(source, viewModel.SelectedSourceNavigation);
        Assert.Equal("Test Playlist B", viewModel.DestinationPlaylistName);
        Assert.Equal(viewModel.Songs.Count(song => song.PlaylistNames.Contains("Test Playlist B")), viewModel.DestinationSongCount);
    }

    [Fact]
    public void DropAddsFixtureMembershipAndPreventsDuplicate()
    {
        var viewModel = ProductionViewModelFixture.Create();
        viewModel.PaneB.SelectedCollection = viewModel.PlaylistNavigation.First(item => item.PlaylistName == "Test Playlist A");
        var song = viewModel.SourceSongsView.Cast<SongItemViewModel>().First(item => !item.PlaylistNames.Contains("Test Playlist A"));
        var before = viewModel.PaneB.VisibleCount;
        var payload = viewModel.PaneA.CreateDragPayload(song);
        viewModel.PaneB.DropCommand.Execute(payload);
        viewModel.PaneB.DropCommand.Execute(payload);

        Assert.Equal(before + 1, viewModel.PaneB.VisibleCount);
        Assert.Single(song.PlaylistNames, name => name == "Test Playlist A");
        Assert.Contains("1曲は既に登録済み", viewModel.StatusMessage);
    }

    [Fact]
    public void UnsortedFavoritesContainOnlyFavoriteSongsWithoutMemberships()
    {
        var viewModel = ProductionViewModelFixture.Create();
        viewModel.SelectedSourceNavigation = new NavigationItemViewModel("未整理", NavigationFilter.UnsortedFavorites);

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
        var viewModel = ProductionViewModelFixture.Create();
        var song = viewModel.Songs.First(item => item.PlaylistNames.Count > 1);

        Assert.True(song.PlaylistNames.Count > 1);
        Assert.Contains("Test Playlist A", viewModel.Songs.First(item => item.Title == "Test Song Beta").PlaylistNames);
    }

    [Fact]
    public void GameRunningDisablesMockEditingCommands()
    {
        var viewModel = ProductionViewModelFixture.Create();
        viewModel.GameState = GameAccessState.RunningReadOnly;
        var song = viewModel.Songs[0];

        Assert.False(viewModel.DropSongCommand.CanExecute(song));
        Assert.False(viewModel.ToggleFavoriteCommand.CanExecute(song));
        Assert.True(viewModel.IsReadOnly);
    }

    [Fact]
    public void CheckboxSelectionIsIndependentForSourceAndDestination()
    {
        var vm = ProductionViewModelFixture.Create(); var song = vm.PaneB.VisibleSongs.Cast<SongItemViewModel>().First();
        song.IsSourceChecked = true;
        Assert.Equal(1, vm.SourceSelectedCount); Assert.Equal(0, vm.DestinationSelectedCount);
        song.IsDestinationChecked = true;
        Assert.Equal(1, vm.SourceSelectedCount); Assert.Equal(1, vm.DestinationSelectedCount);
    }

    [Fact]
    public void SelectAllTargetsOnlyVisibleAndClearRemovesHiddenSelections()
    {
        var vm = ProductionViewModelFixture.Create(); vm.SelectedSourceNavigation = new("すべて", NavigationFilter.AllSongs); vm.SearchText = "Search Mapper";
        vm.SelectAllVisibleSourceCommand.Execute(null);
        Assert.Equal(1, vm.SourceSelectedCount); Assert.Equal(1, vm.VisibleSourceSelectedCount);
        vm.SearchText = "";
        Assert.Equal(1, vm.SourceSelectedCount); Assert.Equal(1, vm.VisibleSourceSelectedCount);
        vm.ClearSourceSelectionCommand.Execute(null); Assert.Equal(0, vm.SourceSelectedCount);
    }

    [Fact]
    public void CheckedSongDragBuildsMultiPayloadButUncheckedDragIsSingle()
    {
        var vm = ProductionViewModelFixture.Create(); vm.SelectedSourceNavigation = new("すべて", NavigationFilter.AllSongs);
        vm.Songs[2].IsSourceChecked = true; vm.Songs[3].IsSourceChecked = true;
        var multiple = vm.CreateSourceDragPayload(vm.Songs[2]); var single = vm.CreateSourceDragPayload(vm.Songs[4]);
        Assert.Equal(2, multiple.Count); Assert.Single(single.Songs); Assert.Same(vm.Songs[4], single.Songs[0]);
        Assert.True(vm.Songs[2].IsSourceChecked); Assert.True(vm.Songs[3].IsSourceChecked);
    }

    [Fact]
    public void BulkAddAddsUniqueSongsAndReportsDuplicates()
    {
        var vm = ProductionViewModelFixture.Create(); vm.SelectedSourceNavigation = new("すべて", NavigationFilter.AllSongs); vm.PaneB.SelectedCollection = vm.PlaylistNavigation.First(x => x.PlaylistName == "Test Playlist A");
        var duplicate = vm.Songs.First(x => x.PlaylistNames.Contains("Test Playlist A")); var newSong = vm.Songs.First(x => !x.PlaylistNames.Contains("Test Playlist A"));
        vm.PaneA.ToggleCheckedCommand.Execute(duplicate); vm.PaneA.ToggleCheckedCommand.Execute(newSong); var before = vm.PaneB.VisibleCount;
        var payload = vm.PaneA.CreateDragPayload(duplicate); vm.PaneB.DropCommand.Execute(payload);
        Assert.Equal(before + 1, vm.PaneB.VisibleCount); Assert.Contains("1曲は既に登録済み", vm.StatusMessage);
    }

    [Fact]
    public void SameSourceAndDestinationDisablesAddAndMove()
    {
        var vm = ProductionViewModelFixture.Create(); var playlist = vm.PlaylistNavigation.First(x => x.PlaylistName == "Test Playlist A"); vm.SelectedSourceNavigation = playlist; vm.PaneB.SelectedCollection = playlist;
        vm.SourceSongsView.Cast<SongItemViewModel>().First().IsSourceChecked = true;
        Assert.True(vm.IsSamePlaylist); Assert.False(vm.BulkAddCommand.CanExecute(null));
    }

    [Fact]
    public void PlaylistCreateAndRenameAreAvailable()
    {
        var vm = ProductionViewModelFixture.Create(); var start = vm.PlaylistCount; vm.PlaylistNameDraft = "New Mix"; vm.CreatePlaylistCommand.Execute(null);
        Assert.Equal(start + 1, vm.PlaylistCount); Assert.Equal("New Mix", vm.DestinationPlaylistName);
        vm.PlaylistNameDraft = "Renamed Mix"; vm.RenamePlaylistCommand.Execute(null); Assert.Equal("Renamed Mix", vm.DestinationPlaylistName);
        Assert.Equal(start + 1, vm.PlaylistCount);
    }

    [Fact]
    public void BulkFavoriteOnAndOffAreIndependentFromPlaylistAdd()
    {
        var vm = ProductionViewModelFixture.Create(); vm.SelectedSourceNavigation = new("すべて", NavigationFilter.AllSongs); var song = vm.Songs.First(x => !x.IsFavorite && x.PlaylistNames.Count == 0); song.IsSourceChecked = true;
        vm.FavoriteBulkOnCommand.Execute(null); Assert.True(song.IsFavorite); vm.FavoriteBulkOffCommand.Execute(null); Assert.False(song.IsFavorite); Assert.DoesNotContain("Test Playlist A", song.PlaylistNames);
    }

    [Fact]
    public void GameRunningDisablesEveryEditingCommandButKeepsChecksAvailable()
    {
        var vm = ProductionViewModelFixture.Create(); vm.SelectedSourceNavigation = new("すべて", NavigationFilter.AllSongs); vm.GameState = GameAccessState.RunningReadOnly; var song = vm.Songs[2]; song.IsSourceChecked = true; vm.PlaylistNameDraft = "Blocked";
        Assert.True(song.IsSourceChecked); Assert.False(vm.BulkAddCommand.CanExecute(null)); Assert.False(vm.FavoriteBulkOnCommand.CanExecute(null)); Assert.False(vm.CreatePlaylistCommand.CanExecute(null));
    }

    [Fact]
    public void AudioPreviewUsesSelectedSongPathAndStopsWhenSelectionChanges()
    {
        var player = new FakeAudioPreviewPlayer();
        using var vm = new MainViewModel(new AudioDataSource(), null, null, null, player);
        vm.SelectedSong = vm.Songs[0];

        Assert.True(vm.TogglePreviewCommand.CanExecute(null));
        vm.TogglePreviewCommand.Execute(null);
        Assert.Equal("preview.ogg", player.LoadedPath);
        Assert.True(vm.IsPreviewPlaying);

        vm.SelectedSong = vm.Songs[1];
        Assert.True(player.StopCount > 0);
        Assert.False(vm.TogglePreviewCommand.CanExecute(null));
        Assert.False(vm.IsPreviewPlaying);
    }

    [Fact]
    public void BlacklistHasPersistentNavigationEntryAndInitiallyNoMockMembers()
    {
        using var vm = ProductionViewModelFixture.Create();
        var blacklist = Assert.Single(vm.SmartNavigation, item => item.Filter == NavigationFilter.Blacklist);
        vm.PaneA.SelectedCollection = blacklist;
        Assert.Equal(0, vm.PaneA.VisibleCount);
    }

    private sealed class AudioDataSource : ILibraryDataSource
    {
        public IReadOnlyList<Song> GetSongs() =>
        [
            new(new(SongKind.Custom, "audio"), "Audio", "Artist", "Mapper", null, TimeSpan.FromMinutes(3), "Unknown", false, [], null,
                HasAudio: true, AudioState: AudioPreviewState.Available, AudioPreviewPath: "preview.ogg"),
            new(new(SongKind.Custom, "missing"), "Missing", "Artist", "Mapper", null, null, "Unknown", false, [], null)
        ];
        public IReadOnlyList<PlaylistSummary> GetPlaylists() => [new("audio-fixture", "Audio Fixture Playlist", 0)];
    }

    private sealed class FakeAudioPreviewPlayer : IAudioPreviewPlayer
    {
        public event EventHandler? PlaybackStateChanged;
        public bool IsPlaying { get; private set; }
        public TimeSpan Position { get; private set; }
        public TimeSpan Duration { get; private set; } = TimeSpan.FromMinutes(3);
        public float Volume { get; set; }
        public string? LoadedPath { get; private set; }
        public int StopCount { get; private set; }
        public void Load(string path) => LoadedPath = path;
        public void Play() { IsPlaying = true; PlaybackStateChanged?.Invoke(this, EventArgs.Empty); }
        public void Pause() { IsPlaying = false; PlaybackStateChanged?.Invoke(this, EventArgs.Empty); }
        public void Seek(TimeSpan position) => Position = position;
        public void Stop() { IsPlaying = false; StopCount++; PlaybackStateChanged?.Invoke(this, EventArgs.Empty); }
        public void Dispose() => Stop();
    }
}
