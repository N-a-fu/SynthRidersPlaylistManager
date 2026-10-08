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
    public void PlaylistNavigationSeparatesAvailableAndBlacklistedCustomCounts()
    {
        using var viewModel = new MainViewModel(new PlaylistCountDataSource());
        var counts = Assert.Single(viewModel.PlaylistNavigation);

        Assert.Equal(10, counts.Count);
        Assert.Equal(2, counts.BlacklistCount);
        Assert.Equal("10 + BL 2", counts.CountDisplay);
        Assert.Equal("Count Playlist  10 + BL 2", counts.DisplayLabel);
    }

    [Theory]
    [InlineData(0, 61, 61, "0 + BL 61")]
    [InlineData(4, 60, 64, "4 + BL 60")]
    public void PlaylistShowsAvailableAndBlacklistedCustomRows(int available, int blacklisted, int visible, string badge)
    {
        using var viewModel = new MainViewModel(new PlaylistCountDataSource(available, blacklisted));
        var playlist = Assert.Single(viewModel.PlaylistNavigation);

        viewModel.PaneA.SelectedCollection = playlist;
        viewModel.PaneB.SelectedCollection = playlist;

        Assert.Equal(badge, playlist.CountDisplay);
        Assert.Equal(visible, viewModel.PaneA.VisibleCount);
        Assert.Equal(visible, viewModel.PaneB.VisibleCount);
        Assert.Equal(blacklisted, viewModel.PaneA.VisibleSongs.Cast<SongItemViewModel>().Count(song => song.IsBlacklisted));
        Assert.Equal(blacklisted, viewModel.PaneB.VisibleSongs.Cast<SongItemViewModel>().Count(song => song.IsBlacklisted));
    }

    [Theory]
    [InlineData(10, 0, "10")]
    [InlineData(10, 2, "10 + BL 2")]
    [InlineData(0, 61, "0 + BL 61")]
    public void PlaylistNavigationFormatsBlacklistCountOnlyWhenPresent(int available, int blacklisted, string expected)
    {
        var item = new NavigationItemViewModel("Playlist", NavigationFilter.Playlist, "Playlist", available)
        {
            BlacklistCount = blacklisted
        };

        Assert.Equal(expected, item.CountDisplay);
        Assert.Equal($"Playlist  {expected}", item.DisplayLabel);
    }

    [Fact]
    public void PlaylistNavigationMovesCountBetweenAvailableAndBlacklistStates()
    {
        using var viewModel = ProductionViewModelFixture.Create();
        var playlist = viewModel.PlaylistNavigation.Single(item => item.PlaylistName == "Test Playlist A");
        var song = viewModel.Songs.First(item => item.PlaylistNames.Contains("Test Playlist A"));
        var availableBefore = playlist.Count;

        viewModel.PaneA.SelectedCollection = viewModel.SmartNavigation.Single(item => item.Filter == NavigationFilter.AllSongs);
        viewModel.PaneA.ToggleCheckedCommand.Execute(song);
        viewModel.PaneA.AddBlacklistCommand.Execute(null);
        Assert.Equal(availableBefore - 1, playlist.Count);
        Assert.Equal(1, playlist.BlacklistCount);

        viewModel.PaneA.SelectedCollection = viewModel.SmartNavigation.Single(item => item.Filter == NavigationFilter.Blacklist);
        viewModel.PaneA.RemoveBlacklistCommand.Execute(null);
        Assert.Equal(availableBefore, playlist.Count);
        Assert.Equal(0, playlist.BlacklistCount);
    }

    [Fact]
    public void UnassignedContainsOnlyCustomSongsWithoutPlaylistMembershipRegardlessOfFavorite()
    {
        using var viewModel = new MainViewModel(new UnassignedDataSource());
        var unassigned = viewModel.SmartNavigation.Single(item => item.Filter == NavigationFilter.Unassigned);

        viewModel.PaneA.SelectedCollection = unassigned;
        Assert.Equal(["Custom Unfavorite", "Custom Favorite"],
            viewModel.PaneA.VisibleSongs.Cast<SongItemViewModel>().Select(song => song.Title));

        viewModel.SelectedSourceNavigation = unassigned;
        Assert.Equal(["Custom Unfavorite", "Custom Favorite"],
            viewModel.SourceSongsView.Cast<SongItemViewModel>().Select(song => song.Title));
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

    private sealed class PlaylistCountDataSource : ILibraryDataSource
    {
        private readonly int _available;
        private readonly int _blacklisted;

        public PlaylistCountDataSource(int available = 10, int blacklisted = 2)
        {
            _available = available;
            _blacklisted = blacklisted;
        }

        public IReadOnlyList<Song> GetSongs()
        {
            var songs = new List<Song>();
            for (var index = 0; index < _available; index++) songs.Add(Create($"normal-{index}", SongKind.Custom, false, ["Count Playlist"]));
            for (var index = 0; index < _blacklisted; index++) songs.Add(Create($"blacklist-{index}", SongKind.Custom, true, ["Count Playlist"]));
            songs.Add(Create("official", SongKind.OfficialOrDlc, false, []));
            return songs;
        }

        public IReadOnlyList<PlaylistSummary> GetPlaylists() => [new("count.playlist", "Count Playlist", 99)];

        private static Song Create(string id, SongKind kind, bool blacklisted, IReadOnlyList<string> playlists) =>
            new(new(kind, id), id, "Artist", "Mapper", null, null, "Fixture", false, playlists, null,
                IsBlacklisted: blacklisted);
    }

    private sealed class UnassignedDataSource : ILibraryDataSource
    {
        public IReadOnlyList<Song> GetSongs() =>
        [
            Song("custom-unfavorite", "Custom Unfavorite", SongKind.Custom, false, []),
            Song("custom-favorite", "Custom Favorite", SongKind.Custom, true, []),
            Song("custom-assigned", "Custom Assigned", SongKind.Custom, false, ["Playlist"]),
            Song("custom-assigned-favorite", "Custom Assigned Favorite", SongKind.Custom, true, ["Playlist"]),
            Song("official", "Official", SongKind.OfficialOrDlc, false, [])
        ];

        public IReadOnlyList<PlaylistSummary> GetPlaylists() => [new("fixture.playlist", "Playlist", 2)];

        private static Song Song(string id, string title, SongKind kind, bool favorite, IReadOnlyList<string> playlists) =>
            new(new(kind, id), title, "Artist", "Mapper", null, null, "Fixture", favorite, playlists, null);
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
