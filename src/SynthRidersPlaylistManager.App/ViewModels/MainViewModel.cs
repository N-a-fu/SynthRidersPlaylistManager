using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using SynthRidersPlaylistManager.App.Mvvm;
using SynthRidersPlaylistManager.App.Services;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Core.Services;

namespace SynthRidersPlaylistManager.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly ICollectionView _sourceSongsView;
    private readonly IEnvironmentDiscoveryService? _environmentDiscovery;
    private readonly ILocationPicker? _locationPicker;
    private readonly IRealLibraryReader? _realLibraryReader;
    private readonly IAudioPreviewPlayer _audioPreviewPlayer;
    private readonly DispatcherTimer _previewTimer;
    private NavigationItemViewModel? _selectedSourceNavigation;
    private NavigationItemViewModel? _selectedDestinationNavigation;
    private SongItemViewModel? _selectedSong;
    private NavigationItemViewModel? _pendingDeletePlaylist;
    private string _searchText = "", _playlistNameDraft = "", _statusMessage = "Phase 1.1 Follow-up — Mock data only";
    private bool _isSettingsOpen, _isMuted, _isPreviewPlaying, _hasMockChanges;
    private double _volume = 68;
    private double _previewPositionSeconds, _previewDurationSeconds = 1;
    private bool _updatingPreviewPosition;
    private string? _loadedPreviewPath;
    private GameAccessState _gameState = GameAccessState.Stopped;
    private PaneSide _activePaneSide = PaneSide.A;
    private bool _isEnvironmentScanRunning;
    private bool _isRealDataMode;
    private string _installSummary = "環境検出待機中 — Phase 2A Read-only / Mock Library";

    public MainViewModel() : this(new MockLibraryDataSource(), null, null, null, null) { }
    public MainViewModel(ILibraryDataSource dataSource) : this(dataSource, null, null, null, null) { }
    public MainViewModel(ILibraryDataSource dataSource, IEnvironmentDiscoveryService? environmentDiscovery, ILocationPicker? locationPicker) : this(dataSource, environmentDiscovery, locationPicker, null, null) { }
    public MainViewModel(ILibraryDataSource dataSource, IEnvironmentDiscoveryService? environmentDiscovery, ILocationPicker? locationPicker, IRealLibraryReader? realLibraryReader, IAudioPreviewPlayer? audioPreviewPlayer = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _environmentDiscovery = environmentDiscovery;
        _locationPicker = locationPicker;
        _realLibraryReader = realLibraryReader;
        _audioPreviewPlayer = audioPreviewPlayer ?? new AudioPreviewPlayer();
        _audioPreviewPlayer.Volume = (float)(_volume / 100);
        _audioPreviewPlayer.PlaybackStateChanged += OnPlaybackStateChanged;
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _previewTimer.Tick += (_, _) => UpdatePreviewPosition();
        Songs = new(dataSource.GetSongs().Select(x => new SongItemViewModel(x)));
        foreach (var song in Songs) song.PropertyChanged += OnSongPropertyChanged;
        Playlists = new(dataSource.GetPlaylists());
        DestinationSongs = [];
        FavoritesNavigation = [new("すべてのお気に入り", NavigationFilter.Favorites), new("未整理", NavigationFilter.UnsortedFavorites), new("Playlist登録済み", NavigationFilter.AssignedFavorites)];
        PlaylistNavigation = new(Playlists.Select(x => new NavigationItemViewModel(x.Name, NavigationFilter.Playlist, x.Name, x.SongCount)));
        SmartNavigation = [new("すべての曲", NavigationFilter.AllSongs), new("未登録", NavigationFilter.Unassigned), new("複数Playlist", NavigationFilter.MultiplePlaylists), new("Custom", NavigationFilter.Custom), new("Official / DLC", NavigationFilter.OfficialOrDlc), new("最近追加", NavigationFilter.RecentlyAdded), new("ブラックリスト", NavigationFilter.Blacklist)];
        _sourceSongsView = CollectionViewSource.GetDefaultView(Songs);
        _sourceSongsView.Filter = FilterSourceSong;

        ToggleFavoriteCommand = _toggleFavorite = new(ToggleFavorite, _ => !IsReadOnly);
        DropSongsCommand = _dropSongs = new(AddPayloadToDestination, CanAcceptPayload);
        DropSongCommand = new RelayCommand<SongItemViewModel>(s => AddPayloadToDestination(CreateSourceDragPayload(s)), s => CanAcceptPayload(CreateSourceDragPayload(s)));
        BulkAddCommand = _bulkAdd = new(() => AddPayloadToDestination(new(SourceCheckedSongs, "Source")), () => CanBulkAdd);
        BulkMoveCommand = _bulkMove = new(MoveCheckedSourceSongs, () => CanBulkMove);
        FavoriteBulkOnCommand = _favoriteOn = new(() => SetBulkFavorite(true), () => CanBulkFavorite);
        FavoriteBulkOffCommand = _favoriteOff = new(() => SetBulkFavorite(false), () => CanBulkFavorite);
        SelectAllVisibleSourceCommand = new RelayCommand(() => SetVisibleSourceChecks(true));
        ClearSourceSelectionCommand = new RelayCommand(() => SetChecks(Songs, true, false));
        SelectAllDestinationCommand = new RelayCommand(() => SetDestinationChecks(true));
        ClearDestinationSelectionCommand = new RelayCommand(() => SetChecks(DestinationSongs, false, false));
        CreatePlaylistCommand = _createPlaylist = new(CreatePlaylist, () => CanManagePlaylists && IsValidNewPlaylistName);
        RenamePlaylistCommand = _renamePlaylist = new(RenameSelectedPlaylist, () => CanManagePlaylists && SelectedDestinationNavigation is not null && IsValidRename);
        DuplicatePlaylistCommand = _duplicatePlaylist = new RelayCommand<NavigationItemViewModel>(DuplicatePlaylist, _ => CanManagePlaylists);
        RequestDeletePlaylistCommand = _requestDelete = new RelayCommand<NavigationItemViewModel>(x => PendingDeletePlaylist = x, _ => CanManagePlaylists && PlaylistNavigation.Count > 1);
        ConfirmDeletePlaylistCommand = _confirmDelete = new(ConfirmDeletePlaylist, () => CanManagePlaylists && PendingDeletePlaylist is not null && PlaylistNavigation.Count > 1);
        CancelDeletePlaylistCommand = new RelayCommand(() => PendingDeletePlaylist = null);
        OpenPlaylistAsSourceCommand = new RelayCommand<NavigationItemViewModel>(x => SelectedSourceNavigation = x);
        SetPlaylistAsDestinationCommand = new RelayCommand<NavigationItemViewModel>(x => SelectedDestinationNavigation = x);
        PrepareRenamePlaylistCommand = new RelayCommand<NavigationItemViewModel>(x => { SelectedDestinationNavigation = x; PlaylistNameDraft = x.PlaylistName ?? x.Label; SetStatusMessage("名前欄を編集し［名前変更］を押してください"); });
        foreach (var playlist in PlaylistNavigation) ConfigurePlaylistCommands(playlist);
        PaneA = new CollectionPaneViewModel(this, PaneSide.A);
        PaneB = new CollectionPaneViewModel(this, PaneSide.B);
        OpenSettingsCommand = new RelayCommand(() => IsSettingsOpen = true);
        CloseSettingsCommand = new RelayCommand(() => IsSettingsOpen = false);
        RescanCommand = new RelayCommand(() => _ = RefreshEnvironmentAsync(), () => _environmentDiscovery is not null && !IsEnvironmentScanRunning);
        BrowseLocationCommand = new RelayCommand<EnvironmentLocationViewModel>(location => _ = BrowseLocationAsync(location), _ => _environmentDiscovery is not null && _locationPicker is not null && !IsEnvironmentScanRunning);
        ToggleGameStateCommand = new RelayCommand(ToggleGameState);
        TogglePreviewCommand = new RelayCommand(TogglePreview, () => CanPreviewSelectedSong);
        ToggleMuteCommand = new RelayCommand(() => IsMuted = !IsMuted);
        SaveChangesCommand = new RelayCommand(() => SetStatusMessage("Mock Changesのみ — 実保存は無効です"));
        PaneA.SelectedCollection = FavoritesNavigation[1];
        PaneB.SelectedCollection = PlaylistNavigation.First(x => x.PlaylistName == "TEST_PLAYLIST");
        SelectedSong = SourceSongsView.Cast<SongItemViewModel>().FirstOrDefault() ?? Songs[0];
    }

    private readonly RelayCommand<SongItemViewModel> _toggleFavorite;
    private readonly RelayCommand<SongDragPayload> _dropSongs;
    private readonly RelayCommand _bulkAdd, _bulkMove, _favoriteOn, _favoriteOff, _createPlaylist, _renamePlaylist, _confirmDelete;
    private readonly RelayCommand<NavigationItemViewModel> _duplicatePlaylist, _requestDelete;
    public ObservableCollection<SongItemViewModel> Songs { get; }
    public ObservableCollection<SongItemViewModel> DestinationSongs { get; }
    public ObservableCollection<PlaylistSummary> Playlists { get; }
    public ObservableCollection<NavigationItemViewModel> FavoritesNavigation { get; }
    public ObservableCollection<NavigationItemViewModel> PlaylistNavigation { get; }
    public ObservableCollection<NavigationItemViewModel> SmartNavigation { get; }
    public ObservableCollection<EnvironmentLocationViewModel> EnvironmentLocations { get; } = [];
    public CollectionPaneViewModel PaneA { get; }
    public CollectionPaneViewModel PaneB { get; }
    public IEnumerable<NavigationItemViewModel> AllCollections => FavoritesNavigation.Concat(PlaylistNavigation).Concat(SmartNavigation);
    public ICollectionView SourceSongsView => _sourceSongsView;
    public ICollectionView SongsView => _sourceSongsView;
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand DropSongCommand { get; }
    public ICommand DropSongsCommand { get; }
    public ICommand BulkAddCommand { get; }
    public ICommand BulkMoveCommand { get; }
    public ICommand FavoriteBulkOnCommand { get; }
    public ICommand FavoriteBulkOffCommand { get; }
    public ICommand SelectAllVisibleSourceCommand { get; }
    public ICommand ClearSourceSelectionCommand { get; }
    public ICommand SelectAllDestinationCommand { get; }
    public ICommand ClearDestinationSelectionCommand { get; }
    public ICommand CreatePlaylistCommand { get; }
    public ICommand RenamePlaylistCommand { get; }
    public ICommand DuplicatePlaylistCommand { get; }
    public ICommand RequestDeletePlaylistCommand { get; }
    public ICommand ConfirmDeletePlaylistCommand { get; }
    public ICommand CancelDeletePlaylistCommand { get; }
    public ICommand OpenPlaylistAsSourceCommand { get; }
    public ICommand SetPlaylistAsDestinationCommand { get; }
    public ICommand PrepareRenamePlaylistCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand CloseSettingsCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand BrowseLocationCommand { get; }
    public ICommand ToggleGameStateCommand { get; }
    public ICommand TogglePreviewCommand { get; }
    public ICommand ToggleMuteCommand { get; }
    public ICommand SaveChangesCommand { get; }

    public int LibraryCount => Songs.Count;
    public int FavoriteCount => Songs.Count(x => x.IsFavorite);
    public int PlaylistCount => Playlists.Count;
    public int VisibleSourceSongCount => _sourceSongsView.Cast<object>().Count();
    public int VisibleSongCount => VisibleSourceSongCount;
    public int DestinationSongCount => DestinationSongs.Count;
    public SongItemViewModel[] SourceCheckedSongs => Songs.Where(x => x.IsSourceChecked).ToArray();
    public SongItemViewModel[] DestinationCheckedSongs => DestinationSongs.Where(x => x.IsDestinationChecked).ToArray();
    public int SourceSelectedCount => SourceCheckedSongs.Length;
    public int VisibleSourceSelectedCount => _sourceSongsView.Cast<SongItemViewModel>().Count(x => x.IsSourceChecked);
    public int DestinationSelectedCount => DestinationCheckedSongs.Length;
    public string SourceSelectionDisplay => $"Source: {SourceSelectedCount}曲選択中（表示中{VisibleSourceSelectedCount}曲）";
    public string DestinationSelectionDisplay => $"Destination: {DestinationSelectedCount}曲選択中";
    public bool HasSourceSelection => SourceSelectedCount > 0;
    public bool HasDestinationSelection => DestinationSelectedCount > 0;
    public bool? AreAllVisibleSourceChecked { get => CheckState(_sourceSongsView.Cast<SongItemViewModel>(), true); set { if (value.HasValue) SetVisibleSourceChecks(value.Value); } }
    public bool? AreAllDestinationChecked { get => CheckState(DestinationSongs, false); set { if (value.HasValue) SetDestinationChecks(value.Value); } }
    public string GameVersion => IsRealDataMode ? "未取得" : "v3.6.5a1（ダミー）";
    public string InstallSummary { get => _installSummary; private set => SetProperty(ref _installSummary, value); }
    public string SourceViewTitle => SelectedSourceNavigation?.Label ?? "Source";
    public string CurrentViewTitle => SourceViewTitle;
    public string DestinationPlaylistName => SelectedDestinationNavigation?.PlaylistName ?? "Playlist未選択";
    public bool IsSamePlaylist => SelectedSourceNavigation?.Filter == NavigationFilter.Playlist && string.Equals(SelectedSourceNavigation.PlaylistName, SelectedDestinationNavigation?.PlaylistName, StringComparison.OrdinalIgnoreCase);
    public string SamePlaylistDisplay => IsSamePlaylist ? "SAME PLAYLIST · Add / Move 無効" : "";
    public bool HasSelectedSong => SelectedSong is not null;
    public bool CanEditDestination => !IsReadOnly && SelectedDestinationNavigation?.PlaylistName is not null && !IsSamePlaylist;
    public bool CanBulkAdd => HasSourceSelection && CanEditDestination;
    public bool CanBulkMove => CanBulkAdd && SelectedSourceNavigation?.Filter == NavigationFilter.Playlist;
    public bool CanBulkFavorite => !IsReadOnly && (HasSourceSelection || HasDestinationSelection);
    public bool CanManagePlaylists => !IsReadOnly;
    public bool IsValidNewPlaylistName => !string.IsNullOrWhiteSpace(PlaylistNameDraft) && !PlaylistNavigation.Any(x => string.Equals(x.PlaylistName, PlaylistNameDraft.Trim(), StringComparison.OrdinalIgnoreCase));
    public bool IsValidRename => IsValidNewPlaylistName || string.Equals(SelectedDestinationNavigation?.PlaylistName, PlaylistNameDraft.Trim(), StringComparison.Ordinal);
    public string PreviewButtonGlyph => IsPreviewPlaying ? "❚❚" : "▶";
    public string MuteButtonGlyph => IsMuted ? "🔇" : "🔊";
    public bool CanPreviewSelectedSong => SelectedSong?.HasAudioPreview == true;
    public string PreviewAvailabilityText => SelectedSong?.AudioState switch
    {
        AudioPreviewState.Available => IsPreviewPlaying ? "再生中" : "Audio Preview",
        AudioPreviewState.Invalid => "音声を読み取れません — Placeholder",
        AudioPreviewState.ParentLocationUnavailable => "Audio保存先を利用できません",
        AudioPreviewState.Unknown => "Audio状態を確認できません",
        _ => SelectedSong?.Identity.Kind == SongKind.Custom
            ? "この曲はまだプレビューできません。ゲーム内で一度プレビュー後、再スキャンしてください。"
            : "この曲のAudio Previewは利用できません。"
    };
    public string PreviewElapsedDisplay => FormatTime(TimeSpan.FromSeconds(PreviewPositionSeconds));
    public string PreviewDurationDisplay => FormatTime(TimeSpan.FromSeconds(PreviewDurationSeconds));
    public string GameStateDisplay => IsRealDataMode ? "REAL DATA · READ ONLY" : GameState == GameAccessState.RunningReadOnly ? "GAME RUNNING · READ ONLY" : "GAME STOPPED · MOCK EDITING";
    public bool IsReadOnly => IsRealDataMode || GameState != GameAccessState.Stopped;
    public string ChangeStateDisplay => IsRealDataMode ? "実データ接続済み · 書込無効" : HasMockChanges ? "● Mock Changes · not saved" : "No Changes · Mock data only";
    public bool IsRealDataMode { get => _isRealDataMode; private set { if (SetProperty(ref _isRealDataMode, value)) { Changed(nameof(IsReadOnly), nameof(IsDragDropEnabled), nameof(CanManagePlaylists), nameof(GameStateDisplay), nameof(ChangeStateDisplay), nameof(GameVersion)); RaiseCommandStates(); ((RelayCommand)TogglePreviewCommand).RaiseCanExecuteChanged(); PaneA.RaiseCommands(); PaneB.RaiseCommands(); } } }
    public bool IsDragDropEnabled => !IsRealDataMode;
    public bool IsDeleteConfirmationOpen => PendingDeletePlaylist is not null;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool HasMockChanges { get => _hasMockChanges; private set { if (SetProperty(ref _hasMockChanges, value)) OnPropertyChanged(nameof(ChangeStateDisplay)); } }
    public NavigationItemViewModel? SelectedNavigation { get => SelectedSourceNavigation; set => SelectedSourceNavigation = value; }
    public NavigationItemViewModel? SelectedSourceNavigation { get => _selectedSourceNavigation; set { if (PaneA.SelectedCollection != value) PaneA.SelectedCollection = value; else ApplyPaneCollection(PaneA, value); } }
    public NavigationItemViewModel? SelectedDestinationNavigation { get => _selectedDestinationNavigation; set { if (PaneB.SelectedCollection != value) PaneB.SelectedCollection = value; else ApplyPaneCollection(PaneB, value); } }
    public SongItemViewModel? SelectedSong { get => _selectedSong; set { if (SetProperty(ref _selectedSong, value)) { ResetPreviewForSelection(); Changed(nameof(HasSelectedSong), nameof(CanPreviewSelectedSong), nameof(PreviewAvailabilityText)); ((RelayCommand)TogglePreviewCommand).RaiseCanExecuteChanged(); } } }
    public NavigationItemViewModel? PendingDeletePlaylist { get => _pendingDeletePlaylist; private set { if (SetProperty(ref _pendingDeletePlaylist, value)) { OnPropertyChanged(nameof(IsDeleteConfirmationOpen)); _confirmDelete.RaiseCanExecuteChanged(); } } }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) RefreshSourceFilter(); } }
    public string PlaylistNameDraft { get => _playlistNameDraft; set { if (SetProperty(ref _playlistNameDraft, value)) { _createPlaylist.RaiseCanExecuteChanged(); _renamePlaylist.RaiseCanExecuteChanged(); } } }
    public bool IsSettingsOpen { get => _isSettingsOpen; set => SetProperty(ref _isSettingsOpen, value); }
    public bool IsMuted { get => _isMuted; set { if (SetProperty(ref _isMuted, value)) { ApplyPlayerVolume(); OnPropertyChanged(nameof(MuteButtonGlyph)); } } }
    public double Volume { get => _volume; set { if (SetProperty(ref _volume, Math.Clamp(value, 0, 100))) ApplyPlayerVolume(); } }
    public GameAccessState GameState { get => _gameState; set { if (SetProperty(ref _gameState, value)) { Changed(nameof(GameStateDisplay), nameof(IsReadOnly), nameof(CanManagePlaylists)); RaiseCommandStates(); } } }
    public bool IsPreviewPlaying { get => _isPreviewPlaying; private set { if (SetProperty(ref _isPreviewPlaying, value)) Changed(nameof(PreviewButtonGlyph), nameof(PreviewAvailabilityText)); } }
    public double PreviewPositionSeconds
    {
        get => _previewPositionSeconds;
        set
        {
            var clamped = Math.Clamp(value, 0, PreviewDurationSeconds);
            if (!SetProperty(ref _previewPositionSeconds, clamped)) return;
            OnPropertyChanged(nameof(PreviewElapsedDisplay));
            if (!_updatingPreviewPosition && _loadedPreviewPath is not null) _audioPreviewPlayer.Seek(TimeSpan.FromSeconds(clamped));
        }
    }
    public double PreviewDurationSeconds { get => _previewDurationSeconds; private set { if (SetProperty(ref _previewDurationSeconds, Math.Max(1, value))) OnPropertyChanged(nameof(PreviewDurationDisplay)); } }
    public bool IsEnvironmentScanRunning
    {
        get => _isEnvironmentScanRunning;
        private set
        {
            if (!SetProperty(ref _isEnvironmentScanRunning, value)) return;
            ((RelayCommand)RescanCommand).RaiseCanExecuteChanged();
            ((RelayCommand<EnvironmentLocationViewModel>)BrowseLocationCommand).RaiseCanExecuteChanged();
        }
    }
    public PaneSide ActivePaneSide
    {
        get => _activePaneSide;
        private set
        {
            if (!SetProperty(ref _activePaneSide, value)) return;
            PaneA.RaiseActiveStateChanged();
            PaneB.RaiseActiveStateChanged();
        }
    }

    public SongDragPayload CreateSourceDragPayload(SongItemViewModel dragged) => new(dragged.IsSourceChecked ? SourceCheckedSongs : [dragged], "Source");
    public SongDragPayload CreateDestinationDragPayload(SongItemViewModel dragged) => new(dragged.IsDestinationChecked ? DestinationCheckedSongs : [dragged], "Destination");

    public void OnPaneCollectionChanged(CollectionPaneViewModel pane) => ApplyPaneCollection(pane, pane.SelectedCollection);
    public void ActivatePane(CollectionPaneViewModel pane) => ActivePaneSide = pane.Side;
    public Task InitializeEnvironmentAsync() => RefreshEnvironmentAsync();
    public bool CanReceiveDrop(CollectionPaneViewModel target)
    {
        var source = target == PaneA ? PaneB : PaneA;
        return !IsReadOnly && target.IsPlaylist && source.SelectedCollection != target.SelectedCollection;
    }
    public bool CanAddToOpposite(CollectionPaneViewModel source) => CanReceiveDrop(source == PaneA ? PaneB : PaneA);
    public bool CanMoveToOpposite(CollectionPaneViewModel source)
    {
        var target = source == PaneA ? PaneB : PaneA;
        return CanReceiveDrop(target) && source.IsPlaylist && target.IsPlaylist;
    }
    public void DropOnPane(CollectionPaneViewModel target, SongDragPayload payload) => AddSongs(target, payload.Songs);
    public void AddToOpposite(CollectionPaneViewModel source, IReadOnlyList<SongItemViewModel> songs) => AddSongs(source == PaneA ? PaneB : PaneA, songs);
    public void MoveToOpposite(CollectionPaneViewModel source, IReadOnlyList<SongItemViewModel> songs)
    {
        var target = source == PaneA ? PaneB : PaneA; if (!CanMoveToOpposite(source) || source.SelectedCollection?.PlaylistName is not string oldName) return;
        AddSongs(target, songs); foreach (var song in songs) song.RemovePlaylist(oldName); MarkChanged(); SetStatusMessage($"{songs.Count}曲を{oldName}から{target.CollectionName}へ移動しました（Mock）"); RefreshBothPanes();
    }
    public void SetPaneFavorites(CollectionPaneViewModel pane, bool value)
    {
        if (IsReadOnly) return; foreach (var song in pane.CheckedSongs) song.IsFavorite = value; if (pane.CheckedCount > 0) MarkChanged(); SetStatusMessage($"{pane.CheckedCount}曲のFavoriteを{(value ? "ON" : "OFF")}にしました（Mock）"); Changed(nameof(FavoriteCount)); RefreshBothPanes();
    }

    private void AddSongs(CollectionPaneViewModel target, IReadOnlyList<SongItemViewModel> songs)
    {
        if (!CanReceiveDrop(target) || target.SelectedCollection?.PlaylistName is not string name) return; var added = 0; var duplicates = 0;
        foreach (var song in songs) { if (song.AddPlaylist(name)) added++; else duplicates++; } if (added > 0) MarkChanged();
        SetStatusMessage($"{added}曲を{name}へ追加しました · {duplicates}曲は既に登録済みです"); RefreshBothPanes();
    }
    private void ApplyPaneCollection(CollectionPaneViewModel pane, NavigationItemViewModel? value)
    {
        if (pane == PaneA) { if (SetProperty(ref _selectedSourceNavigation, value, nameof(SelectedSourceNavigation))) { Changed(nameof(SelectedNavigation), nameof(SourceViewTitle), nameof(CurrentViewTitle)); RefreshSourceFilter(); } }
        else { if (SetProperty(ref _selectedDestinationNavigation, value, nameof(SelectedDestinationNavigation))) { PlaylistNameDraft = value?.PlaylistName ?? ""; Changed(nameof(DestinationPlaylistName)); RefreshDestination(); } }
        RaiseDestinationState(); PaneA.RaiseCommands(); PaneB.RaiseCommands();
    }
    private void RefreshBothPanes() { UpdatePlaylistCounts(); PaneA.Refresh(); PaneB.Refresh(); RefreshDestination(); RefreshSourceFilter(); }

    private bool FilterSourceSong(object item)
    {
        if (item is not SongItemViewModel s) return false;
        if (!string.IsNullOrWhiteSpace(SearchText) && !s.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) && !s.Artist.Contains(SearchText, StringComparison.OrdinalIgnoreCase) && !s.Mapper.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) return false;
        return SelectedSourceNavigation?.Filter switch { NavigationFilter.Favorites => s.IsFavorite, NavigationFilter.UnsortedFavorites => s.IsFavorite && s.PlaylistNames.Count == 0, NavigationFilter.AssignedFavorites => s.IsFavorite && s.PlaylistNames.Count > 0, NavigationFilter.Unassigned => s.PlaylistNames.Count == 0, NavigationFilter.MultiplePlaylists => s.PlaylistNames.Count > 1, NavigationFilter.Custom => s.Identity.Kind == SongKind.Custom, NavigationFilter.OfficialOrDlc => s.Identity.Kind == SongKind.OfficialOrDlc, NavigationFilter.RecentlyAdded => s.AddedAt >= DateTimeOffset.Now.AddDays(-30), NavigationFilter.Playlist => SelectedSourceNavigation.PlaylistName is not null && s.PlaylistNames.Contains(SelectedSourceNavigation.PlaylistName), _ => true };
    }
    private bool CanAcceptPayload(SongDragPayload p) => p.Count > 0 && CanEditDestination;
    private void AddPayloadToDestination(SongDragPayload payload)
    {
        var name = SelectedDestinationNavigation?.PlaylistName; if (name is null || IsReadOnly || IsSamePlaylist) return;
        var added = 0; var duplicates = 0;
        foreach (var song in payload.Songs) { if (!song.AddPlaylist(name)) { duplicates++; continue; } DestinationSongs.Add(song); added++; }
        if (added > 0) MarkChanged();
        SetStatusMessage($"{added}曲を{name}へ追加しました · {duplicates}曲は既に登録済みです"); RefreshMemberships();
    }
    private void MoveCheckedSourceSongs()
    {
        var source = SelectedSourceNavigation?.PlaylistName; if (source is null || !CanBulkMove) return;
        var songs = SourceCheckedSongs; AddPayloadToDestination(new(songs, "Source")); foreach (var song in songs) song.RemovePlaylist(source);
        MarkChanged(); SetStatusMessage($"{songs.Length}曲を{source}から{DestinationPlaylistName}へ移動しました（Mock）"); RefreshMemberships();
    }
    private void SetBulkFavorite(bool value)
    {
        if (IsReadOnly) return; var selected = SourceCheckedSongs.Concat(DestinationCheckedSongs).Distinct().ToArray();
        foreach (var song in selected) song.IsFavorite = value; if (selected.Length > 0) MarkChanged();
        SetStatusMessage($"{selected.Length}曲のFavoriteを{(value ? "ON" : "OFF")}にしました（Mock）"); Changed(nameof(FavoriteCount)); RefreshSourceFilter();
    }
    private void ToggleFavorite(SongItemViewModel song) { song.IsFavorite = !song.IsFavorite; MarkChanged(); SetStatusMessage($"{song.Title} のFavoriteを{(song.IsFavorite ? "ON" : "OFF")}にしました（Mock）"); Changed(nameof(FavoriteCount)); RefreshSourceFilter(); }
    private void SetVisibleSourceChecks(bool value) => SetChecks(_sourceSongsView.Cast<SongItemViewModel>().ToArray(), true, value);
    private void SetDestinationChecks(bool value) => SetChecks(DestinationSongs, false, value);
    private void SetChecks(IEnumerable<SongItemViewModel> songs, bool source, bool value) { foreach (var song in songs) { if (source) song.IsSourceChecked = value; else song.IsDestinationChecked = value; } RaiseSelectionState(); }
    private static bool? CheckState(IEnumerable<SongItemViewModel> songs, bool source) { var rows = songs.ToArray(); if (rows.Length == 0) return false; var count = rows.Count(x => source ? x.IsSourceChecked : x.IsDestinationChecked); return count == 0 ? false : count == rows.Length ? true : null; }

    private void CreatePlaylist()
    {
        var name = PlaylistNameDraft.Trim(); if (!IsValidNewPlaylistName) return; var item = new NavigationItemViewModel(name, NavigationFilter.Playlist, name, 0);
        ConfigurePlaylistCommands(item); PlaylistNavigation.Add(item); Playlists.Add(new($"mock-{Guid.NewGuid():N}", name, 0)); SelectedDestinationNavigation = item; MarkChanged(); SetStatusMessage($"Playlist「{name}」を作成しました（Mock）");
    }
    private void RenameSelectedPlaylist()
    {
        var item = SelectedDestinationNavigation; if (item?.PlaylistName is null || !IsValidRename) return; var old = item.PlaylistName; var name = PlaylistNameDraft.Trim(); if (old == name) return;
        foreach (var song in Songs) song.RenamePlaylist(old, name); var i = Playlists.ToList().FindIndex(x => x.Name == old); if (i >= 0) Playlists[i] = Playlists[i] with { Name = name };
        item.Label = name; item.PlaylistName = name; Changed(nameof(DestinationPlaylistName)); MarkChanged(); RefreshSourceFilter(); SetStatusMessage($"{old} を {name} へ名前変更しました（Mock）");
    }
    private void DuplicatePlaylist(NavigationItemViewModel item)
    {
        if (item.PlaylistName is null || IsReadOnly) return; var root = $"{item.PlaylistName} Copy"; var name = root; var n = 2; while (PlaylistNavigation.Any(x => string.Equals(x.PlaylistName, name, StringComparison.OrdinalIgnoreCase))) name = $"{root} {n++}";
        var members = Songs.Where(x => x.PlaylistNames.Contains(item.PlaylistName)).ToArray(); foreach (var song in members) song.AddPlaylist(name); var copy = new NavigationItemViewModel(name, NavigationFilter.Playlist, name, members.Length);
        ConfigurePlaylistCommands(copy); PlaylistNavigation.Add(copy); Playlists.Add(new($"mock-{Guid.NewGuid():N}", name, members.Length)); SelectedDestinationNavigation = copy; MarkChanged(); SetStatusMessage($"{item.PlaylistName} を {name} として複製しました（Mock）");
    }
    private void ConfirmDeletePlaylist()
    {
        var item = PendingDeletePlaylist; if (item?.PlaylistName is null || IsReadOnly || PlaylistNavigation.Count <= 1) return; var name = item.PlaylistName;
        foreach (var song in Songs) song.RemovePlaylist(name); PlaylistNavigation.Remove(item); var summary = Playlists.FirstOrDefault(x => x.Name == name); if (summary is not null) Playlists.Remove(summary);
        if (SelectedSourceNavigation == item) SelectedSourceNavigation = SmartNavigation[0]; if (SelectedDestinationNavigation == item) SelectedDestinationNavigation = PlaylistNavigation[0]; PendingDeletePlaylist = null; MarkChanged(); SetStatusMessage($"{name} を削除しました（Mock）");
    }
    private void ConfigurePlaylistCommands(NavigationItemViewModel item)
    {
        item.OpenAsSourceCommand = new RelayCommand(() => SelectedSourceNavigation = item);
        item.SetAsDestinationCommand = new RelayCommand(() => SelectedDestinationNavigation = item);
        item.PrepareRenameCommand = new RelayCommand(() => { SelectedDestinationNavigation = item; PlaylistNameDraft = item.PlaylistName ?? item.Label; SetStatusMessage("名前欄を編集し［名前変更］を押してください"); });
        item.DuplicateCommand = new RelayCommand(() => DuplicatePlaylistCommand.Execute(item), () => DuplicatePlaylistCommand.CanExecute(item));
        item.RequestDeleteCommand = new RelayCommand(() => RequestDeletePlaylistCommand.Execute(item), () => RequestDeletePlaylistCommand.CanExecute(item));
    }
    private void RefreshSourceFilter() { _sourceSongsView.Refresh(); Changed(nameof(VisibleSourceSongCount), nameof(VisibleSongCount)); RaiseSelectionState(); }
    private void RefreshDestination() { DestinationSongs.Clear(); var name = SelectedDestinationNavigation?.PlaylistName; if (name is not null) foreach (var song in Songs.Where(x => x.PlaylistNames.Contains(name))) DestinationSongs.Add(song); Changed(nameof(DestinationSongCount)); RaiseSelectionState(); }
    private void RefreshMemberships() { UpdatePlaylistCounts(); Changed(nameof(DestinationSongCount)); RefreshSourceFilter(); }
    private void UpdatePlaylistCounts() { foreach (var item in PlaylistNavigation) item.Count = Songs.Count(x => item.PlaylistName is not null && x.PlaylistNames.Contains(item.PlaylistName)); for (var i = 0; i < Playlists.Count; i++) Playlists[i] = Playlists[i] with { SongCount = Songs.Count(x => x.PlaylistNames.Contains(Playlists[i].Name)) }; Changed(nameof(PlaylistCount)); }
    private void OnSongPropertyChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName is nameof(SongItemViewModel.IsSourceChecked) or nameof(SongItemViewModel.IsDestinationChecked)) RaiseSelectionState(); }
    private void RaiseSelectionState() { Changed(nameof(SourceSelectedCount), nameof(VisibleSourceSelectedCount), nameof(DestinationSelectedCount), nameof(SourceSelectionDisplay), nameof(DestinationSelectionDisplay), nameof(HasSourceSelection), nameof(HasDestinationSelection), nameof(AreAllVisibleSourceChecked), nameof(AreAllDestinationChecked), nameof(CanBulkAdd), nameof(CanBulkMove), nameof(CanBulkFavorite)); RaiseCommandStates(); }
    private void RaiseDestinationState() { Changed(nameof(IsSamePlaylist), nameof(SamePlaylistDisplay), nameof(CanEditDestination), nameof(CanBulkAdd), nameof(CanBulkMove)); RaiseCommandStates(); }
    private void RaiseCommandStates() { _toggleFavorite.RaiseCanExecuteChanged(); _dropSongs.RaiseCanExecuteChanged(); _bulkAdd.RaiseCanExecuteChanged(); _bulkMove.RaiseCanExecuteChanged(); _favoriteOn.RaiseCanExecuteChanged(); _favoriteOff.RaiseCanExecuteChanged(); _createPlaylist.RaiseCanExecuteChanged(); _renamePlaylist.RaiseCanExecuteChanged(); _duplicatePlaylist.RaiseCanExecuteChanged(); _requestDelete.RaiseCanExecuteChanged(); _confirmDelete.RaiseCanExecuteChanged(); }
    private void ToggleGameState() => GameState = GameState == GameAccessState.RunningReadOnly ? GameAccessState.Stopped : GameAccessState.RunningReadOnly;
    private void TogglePreview()
    {
        if (!CanPreviewSelectedSong || SelectedSong?.AudioPreviewPath is not string path) return;
        try
        {
            if (IsPreviewPlaying)
            {
                _audioPreviewPlayer.Pause();
                IsPreviewPlaying = false;
                _previewTimer.Stop();
                return;
            }

            if (!string.Equals(_loadedPreviewPath, path, StringComparison.OrdinalIgnoreCase))
            {
                _audioPreviewPlayer.Load(path);
                _loadedPreviewPath = path;
                PreviewDurationSeconds = _audioPreviewPlayer.Duration.TotalSeconds;
                PreviewPositionSeconds = 0;
            }
            _audioPreviewPlayer.Play();
            IsPreviewPlaying = true;
            _previewTimer.Start();
        }
        catch (Exception)
        {
            StopPreview();
            SetStatusMessage("Audio Previewを開始できませんでした。曲情報とゲームデータは変更していません。");
        }
    }

    private void ResetPreviewForSelection()
    {
        StopPreview();
        PreviewDurationSeconds = SelectedSong?.Duration?.TotalSeconds ?? 1;
        PreviewPositionSeconds = 0;
    }

    private void StopPreview()
    {
        _previewTimer?.Stop();
        _audioPreviewPlayer.Stop();
        _loadedPreviewPath = null;
        IsPreviewPlaying = false;
    }

    private void UpdatePreviewPosition()
    {
        _updatingPreviewPosition = true;
        try { PreviewPositionSeconds = _audioPreviewPlayer.Position.TotalSeconds; }
        finally { _updatingPreviewPosition = false; }
        if (!_audioPreviewPlayer.IsPlaying)
        {
            IsPreviewPlaying = false;
            _previewTimer.Stop();
        }
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess()) dispatcher.BeginInvoke(UpdatePlaybackState);
        else UpdatePlaybackState();
    }

    private void UpdatePlaybackState()
    {
        IsPreviewPlaying = _audioPreviewPlayer.IsPlaying;
        if (!IsPreviewPlaying) _previewTimer.Stop();
    }

    private void ApplyPlayerVolume() => _audioPreviewPlayer.Volume = IsMuted ? 0 : (float)(Volume / 100);
    private static string FormatTime(TimeSpan value) => $"{(int)value.TotalMinutes}:{value.Seconds:00}";
    private async Task RefreshEnvironmentAsync()
    {
        if (_environmentDiscovery is null || IsEnvironmentScanRunning) return;
        IsEnvironmentScanRunning = true;
        try
        {
            var result = await _environmentDiscovery.DiscoverAsync();
            ApplyEnvironmentResult(result);
            await TryLoadRealLibraryAsync(result);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            InstallSummary = "環境検出中にアクセスエラーが発生しました。Mock UIは使用できます。ゲームデータは変更していません。";
            SetStatusMessage(InstallSummary);
        }
        finally { IsEnvironmentScanRunning = false; }
    }

    private async Task BrowseLocationAsync(EnvironmentLocationViewModel location)
    {
        if (_environmentDiscovery is null || _locationPicker is null || IsEnvironmentScanRunning) return;
        var selected = _locationPicker.Pick(location.Kind, location.PathDisplay == "未解決" ? null : location.PathDisplay);
        if (string.IsNullOrWhiteSpace(selected)) return;
        IsEnvironmentScanRunning = true;
        try
        {
            var result = await _environmentDiscovery.SetManualOverrideAsync(location.Kind, selected);
            ApplyEnvironmentResult(result);
            await TryLoadRealLibraryAsync(result);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            SetStatusMessage("選択したLocationを検証できませんでした。既存設定とゲームデータは変更していません。");
        }
        finally { IsEnvironmentScanRunning = false; }
    }

    private void ApplyEnvironmentResult(EnvironmentDiscoveryResult result)
    {
        foreach (var location in result.Locations)
        {
            var existing = EnvironmentLocations.FirstOrDefault(x => x.Kind == location.Kind);
            if (existing is null) EnvironmentLocations.Add(new(location)); else existing.Update(location);
        }
        InstallSummary = result.Summary;
        SetStatusMessage(result.Summary);
    }
    private async Task TryLoadRealLibraryAsync(EnvironmentDiscoveryResult environment)
    {
        if (_realLibraryReader is null) return;
        try
        {
            var snapshot = await _realLibraryReader.LoadAsync(environment);
            foreach (var song in Songs) song.PropertyChanged -= OnSongPropertyChanged;
            Songs.Clear();
            foreach (var model in snapshot.Songs)
            {
                var song = new SongItemViewModel(model);
                song.PropertyChanged += OnSongPropertyChanged;
                Songs.Add(song);
            }
            Playlists.Clear();
            PlaylistNavigation.Clear();
            foreach (var playlist in snapshot.Playlists)
            {
                Playlists.Add(playlist);
                var navigation = new NavigationItemViewModel(playlist.Name, NavigationFilter.Playlist, playlist.Name, playlist.SongCount);
                ConfigurePlaylistCommands(navigation);
                PlaylistNavigation.Add(navigation);
            }
            var official = SmartNavigation.First(x => x.Filter == NavigationFilter.OfficialOrDlc);
            official.Label = "Official-side / Unknown";
            IsRealDataMode = true;
            HasMockChanges = false;
            PaneA.SelectedCollection = SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs);
            PaneB.SelectedCollection = PlaylistNavigation.FirstOrDefault() ?? SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs);
            SelectedSong = Songs.FirstOrDefault();
            RefreshBothPanes();
            Changed(nameof(LibraryCount), nameof(FavoriteCount), nameof(PlaylistCount));
            var d = snapshot.Diagnostics;
            InstallSummary = $"実データ接続済み · 読み取り専用 · {d.TotalSongs}曲 / Favorite {d.FavoritesResolved}件 / Playlist {snapshot.Playlists.Count}件";
            SetStatusMessage(snapshot.Warnings.Count == 0 ? InstallSummary : $"{InstallSummary} · Warning {snapshot.Warnings.Count}件（データ変更なし）");
        }
        catch (Exception)
        {
            IsRealDataMode = false;
            InstallSummary = "実Libraryを安全に読み込めなかったためMock Dataを表示しています。ゲームデータは変更していません。";
            SetStatusMessage(InstallSummary);
        }
    }
    private void MarkChanged() => HasMockChanges = true;
    private void SetStatusMessage(string value) => StatusMessage = value;
    private void Changed(params string[] names) { foreach (var name in names) OnPropertyChanged(name); }
    public void Dispose()
    {
        _previewTimer.Stop();
        _audioPreviewPlayer.PlaybackStateChanged -= OnPlaybackStateChanged;
        _audioPreviewPlayer.Dispose();
    }
}
