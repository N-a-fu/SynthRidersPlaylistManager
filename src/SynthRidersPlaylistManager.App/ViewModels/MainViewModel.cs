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
    private readonly ISongFlagsStore? _songFlagsStore;
    private readonly IPlaylistStore? _playlistStore;
    private string? _gameRoot;
    private string? _favoritesPath;
    private string? _playlistsPath;
    private readonly Dictionary<string, string> _playlistFilesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAudioPreviewPlayer _audioPreviewPlayer;
    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _gameStateTimer;
    private NavigationItemViewModel? _selectedSourceNavigation;
    private NavigationItemViewModel? _selectedDestinationNavigation;
    private SongItemViewModel? _selectedSong;
    private NavigationItemViewModel? _pendingDeletePlaylist;
    private string _searchText = "", _playlistNameDraft = "", _statusMessage = "";
    private bool _isSettingsOpen, _isMuted, _isPreviewPlaying;
    private double _volume = 68;
    private double _previewPositionSeconds, _previewDurationSeconds = 1;
    private bool _updatingPreviewPosition;
    private string? _loadedPreviewPath;
    private GameAccessState _gameState = GameAccessState.Stopped;
    private PaneSide _activePaneSide = PaneSide.A;
    private bool _isEnvironmentScanRunning;
    private bool _isRealDataMode;
    private bool _suppressLegacySelectionUpdate;
    private string _installSummary = "環境検出待機中";

    public MainViewModel() : this(new MockLibraryDataSource(), null, null, null, null) { }
    public MainViewModel(ILibraryDataSource dataSource) : this(dataSource, null, null, null, null) { }
    public MainViewModel(ILibraryDataSource dataSource, IEnvironmentDiscoveryService? environmentDiscovery, ILocationPicker? locationPicker) : this(dataSource, environmentDiscovery, locationPicker, null, null) { }
    public MainViewModel(ILibraryDataSource dataSource, IEnvironmentDiscoveryService? environmentDiscovery, ILocationPicker? locationPicker, IRealLibraryReader? realLibraryReader, IAudioPreviewPlayer? audioPreviewPlayer = null, ISongFlagsStore? songFlagsStore = null, IPlaylistStore? playlistStore = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _environmentDiscovery = environmentDiscovery;
        _locationPicker = locationPicker;
        _realLibraryReader = realLibraryReader;
        _songFlagsStore = songFlagsStore;
        _playlistStore = playlistStore;
        _audioPreviewPlayer = audioPreviewPlayer ?? new AudioPreviewPlayer();
        _audioPreviewPlayer.Volume = (float)(_volume / 100);
        _audioPreviewPlayer.PlaybackStateChanged += OnPlaybackStateChanged;
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _previewTimer.Tick += (_, _) => UpdatePreviewPosition();
        Songs = new(dataSource.GetSongs().Select(x => new SongItemViewModel(x)));
        foreach (var song in Songs) song.PropertyChanged += OnSongPropertyChanged;
        Playlists = new(dataSource.GetPlaylists());
        foreach (var playlist in Playlists) _playlistFilesByName[playlist.Name] = playlist.Id;
        DestinationSongs = [];
        FavoritesNavigation = [new(LocalizedNavigationLabel(NavigationFilter.Favorites), NavigationFilter.Favorites), new(LocalizedNavigationLabel(NavigationFilter.UnsortedFavorites), NavigationFilter.UnsortedFavorites), new(LocalizedNavigationLabel(NavigationFilter.AssignedFavorites), NavigationFilter.AssignedFavorites)];
        PlaylistNavigation = new(Playlists.Select(x => new NavigationItemViewModel(x.Name, NavigationFilter.Playlist, x.Name, x.SongCount)));
        SmartNavigation = [new(LocalizedNavigationLabel(NavigationFilter.AllSongs), NavigationFilter.AllSongs), new(LocalizedNavigationLabel(NavigationFilter.Unassigned), NavigationFilter.Unassigned), new(LocalizedNavigationLabel(NavigationFilter.MultiplePlaylists), NavigationFilter.MultiplePlaylists), new(LocalizedNavigationLabel(NavigationFilter.Custom), NavigationFilter.Custom), new(LocalizedNavigationLabel(NavigationFilter.OfficialOrDlc), NavigationFilter.OfficialOrDlc), new(LocalizedNavigationLabel(NavigationFilter.RecentlyAdded), NavigationFilter.RecentlyAdded), new(LocalizedNavigationLabel(NavigationFilter.Blacklist), NavigationFilter.Blacklist)];
        _sourceSongsView = CollectionViewSource.GetDefaultView(Songs);
        _sourceSongsView.Filter = FilterSourceSong;

        ToggleFavoriteCommand = _toggleFavorite = new(ToggleFavorite, CanEditFavorite);
        DropSongsCommand = _dropSongs = new(AddPayloadToDestination, CanAcceptPayload);
        DropSongCommand = new RelayCommand<SongItemViewModel>(s => AddPayloadToDestination(CreateSourceDragPayload(s)), s => CanAcceptPayload(CreateSourceDragPayload(s)));
        BulkAddCommand = _bulkAdd = new(() => AddPayloadToDestination(new(SourceCheckedSongs, "Source")), () => CanBulkAdd);
        FavoriteBulkOnCommand = _favoriteOn = new(() => SetBulkFavorite(true), () => CanBulkFavorite);
        FavoriteBulkOffCommand = _favoriteOff = new(() => SetBulkFavorite(false), () => CanBulkFavorite);
        SelectAllVisibleSourceCommand = new RelayCommand(() => SetVisibleSourceChecks(true));
        ClearSourceSelectionCommand = new RelayCommand(() => SetChecks(Songs, true, false));
        SelectAllDestinationCommand = new RelayCommand(() => SetDestinationChecks(true));
        ClearDestinationSelectionCommand = new RelayCommand(() => SetChecks(DestinationSongs, false, false));
        CreatePlaylistCommand = _createPlaylist = new(CreatePlaylist, () => CanManagePlaylists && IsValidNewPlaylistName);
        RenamePlaylistCommand = _renamePlaylist = new(RenameSelectedPlaylist, () => CanManagePlaylists && SelectedDestinationNavigation is not null && IsValidRename);
        RequestDeletePlaylistCommand = _requestDelete = new RelayCommand<NavigationItemViewModel>(x => PendingDeletePlaylist = x, _ => CanManagePlaylists);
        ConfirmDeletePlaylistCommand = _confirmDelete = new(ConfirmDeletePlaylist, () => CanManagePlaylists && PendingDeletePlaylist is not null);
        CancelDeletePlaylistCommand = new RelayCommand(() => PendingDeletePlaylist = null);
        OpenPlaylistAsSourceCommand = new RelayCommand<NavigationItemViewModel>(x => SelectedSourceNavigation = x);
        SetPlaylistAsDestinationCommand = new RelayCommand<NavigationItemViewModel>(x => SelectedDestinationNavigation = x);
        PrepareRenamePlaylistCommand = new RelayCommand<NavigationItemViewModel>(x => { SelectedDestinationNavigation = x; PlaylistNameDraft = x.PlaylistName ?? x.Label; SetStatusMessage("名前欄を編集し［名前変更］を押してください"); });
        foreach (var playlist in PlaylistNavigation) ConfigurePlaylistCommands(playlist);
        PaneA = new CollectionPaneViewModel(this, PaneSide.A);
        PaneB = new CollectionPaneViewModel(this, PaneSide.B);
        OpenSettingsCommand = new RelayCommand(() => IsSettingsOpen = true);
        CloseSettingsCommand = new RelayCommand(() => IsSettingsOpen = false);
        ToggleLanguageCommand = new RelayCommand(ToggleLanguage);
        RescanCommand = new RelayCommand(() => _ = RefreshEnvironmentAsync(), () => _environmentDiscovery is not null && !IsEnvironmentScanRunning);
        BrowseLocationCommand = new RelayCommand<EnvironmentLocationViewModel>(location => _ = BrowseLocationAsync(location), _ => _environmentDiscovery is not null && _locationPicker is not null && !IsEnvironmentScanRunning);
        ToggleGameStateCommand = new RelayCommand(ToggleGameState);
        TogglePreviewCommand = new RelayCommand(TogglePreview, () => CanPreviewSelectedSong);
        ToggleMuteCommand = new RelayCommand(() => IsMuted = !IsMuted);
        PaneA.SelectedCollection = FavoritesNavigation[1];
        PaneB.SelectedCollection = SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs);
        SelectedSong = SourceSongsView.Cast<SongItemViewModel>().FirstOrDefault();
        _gameStateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _gameStateTimer.Tick += (_, _) => UpdateGameState();
        if (_songFlagsStore is not null) _gameStateTimer.Start();
    }

    private readonly RelayCommand<SongItemViewModel> _toggleFavorite;
    private readonly RelayCommand<SongDragPayload> _dropSongs;
    private readonly RelayCommand _bulkAdd, _favoriteOn, _favoriteOff, _createPlaylist, _renamePlaylist, _confirmDelete;
    private readonly RelayCommand<NavigationItemViewModel> _requestDelete;
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
    public ICommand FavoriteBulkOnCommand { get; }
    public ICommand FavoriteBulkOffCommand { get; }
    public ICommand SelectAllVisibleSourceCommand { get; }
    public ICommand ClearSourceSelectionCommand { get; }
    public ICommand SelectAllDestinationCommand { get; }
    public ICommand ClearDestinationSelectionCommand { get; }
    public ICommand CreatePlaylistCommand { get; }
    public ICommand RenamePlaylistCommand { get; }
    public ICommand RequestDeletePlaylistCommand { get; }
    public ICommand ConfirmDeletePlaylistCommand { get; }
    public ICommand CancelDeletePlaylistCommand { get; }
    public ICommand OpenPlaylistAsSourceCommand { get; }
    public ICommand SetPlaylistAsDestinationCommand { get; }
    public ICommand PrepareRenamePlaylistCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand CloseSettingsCommand { get; }
    public ICommand ToggleLanguageCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand BrowseLocationCommand { get; }
    public ICommand ToggleGameStateCommand { get; }
    public ICommand TogglePreviewCommand { get; }
    public ICommand ToggleMuteCommand { get; }

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
    public string SelectedSongTitleDisplay => SelectedSong?.Title ?? UiText("Mini.SelectSong");
    public bool CanEditDestination => CanManagePlaylists && SelectedDestinationNavigation?.PlaylistName is not null && !IsSamePlaylist;
    public bool CanBulkAdd => HasSourceSelection && CanEditDestination;
    public bool CanBulkFavorite => CanSetFavorites(SourceCheckedSongs.Concat(DestinationCheckedSongs).Distinct().ToArray());
    public bool CanManagePlaylists => GameState == GameAccessState.Stopped && !IsEnvironmentScanRunning && (!IsRealDataMode || (_playlistsPath is not null && _playlistStore?.IsGameStopped == true));
    public bool IsValidNewPlaylistName => IsSafePlaylistName(PlaylistNameDraft) && !PlaylistNavigation.Any(x => string.Equals(x.PlaylistName, PlaylistNameDraft.Trim(), StringComparison.OrdinalIgnoreCase));
    public bool IsValidRename => IsValidNewPlaylistName || string.Equals(SelectedDestinationNavigation?.PlaylistName, PlaylistNameDraft.Trim(), StringComparison.Ordinal);
    public string PreviewButtonGlyph => IsPreviewPlaying ? "❚❚" : "▶";
    public string MuteButtonGlyph => IsMuted ? "🔇" : "🔊";
    public bool CanPreviewSelectedSong => SelectedSong?.HasAudioPreview == true;
    public string PreviewAvailabilityText => SelectedSong?.AudioState switch
    {
        AudioPreviewState.Available => UiText(IsPreviewPlaying ? "Audio.Playing" : "Audio.Available"),
        AudioPreviewState.Invalid => UiText("Audio.Invalid"),
        AudioPreviewState.ParentLocationUnavailable => UiText("Audio.ParentUnavailable"),
        AudioPreviewState.Unknown => UiText("Audio.Unknown"),
        _ => SelectedSong?.Identity.Kind == SongKind.Custom
            ? UiText("Audio.CustomUnavailable")
            : UiText("Audio.Unavailable")
    };
    public string PreviewElapsedDisplay => FormatTime(TimeSpan.FromSeconds(PreviewPositionSeconds));
    public string PreviewDurationDisplay => FormatTime(TimeSpan.FromSeconds(PreviewDurationSeconds));
    public string GameStateDisplay => GameState == GameAccessState.RunningReadOnly ? UiText("State.GameRunning") : IsRealDataMode && _songFlagsStore is not null ? UiText("BlacklistOnly") : UiText("State.RealReadOnly");
    public bool IsReadOnly => IsRealDataMode || GameState != GameAccessState.Stopped;
    public string ChangeStateDisplay => IsRealDataMode ? _songFlagsStore is not null && GameState == GameAccessState.Stopped ? UiText("BlacklistOnly") : UiText("State.RealWriteDisabled") : "";
    public bool IsRealDataMode { get => _isRealDataMode; private set { if (SetProperty(ref _isRealDataMode, value)) { Changed(nameof(IsReadOnly), nameof(IsDragDropEnabled), nameof(CanManagePlaylists), nameof(GameStateDisplay), nameof(ChangeStateDisplay), nameof(GameVersion)); RaiseCommandStates(); ((RelayCommand)TogglePreviewCommand).RaiseCanExecuteChanged(); PaneA.RaiseCommands(); PaneB.RaiseCommands(); } } }
    public bool IsDragDropEnabled => GameState == GameAccessState.Stopped;
    public bool IsDeleteConfirmationOpen => PendingDeletePlaylist is not null;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public NavigationItemViewModel? SelectedNavigation { get => SelectedSourceNavigation; set => SelectedSourceNavigation = value; }
    public NavigationItemViewModel? SelectedSourceNavigation { get => _selectedSourceNavigation; set { if (PaneA.SelectedCollection != value) PaneA.SelectedCollection = value; else ApplyPaneCollection(PaneA, value); } }
    public NavigationItemViewModel? SelectedDestinationNavigation { get => _selectedDestinationNavigation; set { if (PaneB.SelectedCollection != value) PaneB.SelectedCollection = value; else ApplyPaneCollection(PaneB, value); } }
    public SongItemViewModel? SelectedSong { get => _selectedSong; set { if (SetProperty(ref _selectedSong, value)) { ResetPreviewForSelection(); Changed(nameof(HasSelectedSong), nameof(SelectedSongTitleDisplay), nameof(CanPreviewSelectedSong), nameof(PreviewAvailabilityText)); ((RelayCommand)TogglePreviewCommand).RaiseCanExecuteChanged(); } } }
    public NavigationItemViewModel? PendingDeletePlaylist { get => _pendingDeletePlaylist; private set { if (SetProperty(ref _pendingDeletePlaylist, value)) { OnPropertyChanged(nameof(IsDeleteConfirmationOpen)); _confirmDelete.RaiseCanExecuteChanged(); } } }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) RefreshSourceFilter(); } }
    public string PlaylistNameDraft { get => _playlistNameDraft; set { if (SetProperty(ref _playlistNameDraft, value)) { _createPlaylist.RaiseCanExecuteChanged(); _renamePlaylist.RaiseCanExecuteChanged(); } } }
    public bool IsSettingsOpen { get => _isSettingsOpen; set => SetProperty(ref _isSettingsOpen, value); }
    public bool IsMuted { get => _isMuted; set { if (SetProperty(ref _isMuted, value)) { ApplyPlayerVolume(); OnPropertyChanged(nameof(MuteButtonGlyph)); } } }
    public double Volume { get => _volume; set { if (SetProperty(ref _volume, Math.Clamp(value, 0, 100))) ApplyPlayerVolume(); } }
    public GameAccessState GameState { get => _gameState; set { if (SetProperty(ref _gameState, value)) { Changed(nameof(GameStateDisplay), nameof(ChangeStateDisplay), nameof(IsReadOnly), nameof(IsDragDropEnabled), nameof(CanManagePlaylists)); RaiseCommandStates(); PaneA.RaiseCommands(); PaneB.RaiseCommands(); } } }
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
            Changed(nameof(CanManagePlaylists));
            RaiseCommandStates();
            PaneA.RaiseCommands(); PaneB.RaiseCommands();
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
    private void UpdateGameState()
    {
        if (!IsRealDataMode || _songFlagsStore is null) return;
        GameState = _songFlagsStore.IsGameStopped ? GameAccessState.Stopped : GameAccessState.RunningReadOnly;
        RaiseCommandStates(); PaneA.RaiseCommands(); PaneB.RaiseCommands();
    }
    public bool CanReceiveDrop(CollectionPaneViewModel target)
    {
        return CanAddBetweenPanes(target);
    }
    private CollectionPaneViewModel OppositePane(CollectionPaneViewModel pane) => pane == PaneA ? PaneB : PaneA;
    private bool CanAddBetweenPanes(CollectionPaneViewModel target)
    {
        if (target != PaneA && target != PaneB) return false;
        var source = OppositePane(target);
        return CanManagePlaylists && target.IsPlaylist && !string.IsNullOrWhiteSpace(target.SelectedCollection?.PlaylistName) &&
            (!source.IsPlaylist || !string.Equals(source.SelectedCollection?.PlaylistName, target.SelectedCollection?.PlaylistName, StringComparison.OrdinalIgnoreCase));
    }
    public bool CanDropOnPane(CollectionPaneViewModel target, SongDragPayload payload)
    {
        if (!CanReceiveDrop(target) || payload.Count == 0) return false;
        var source = OppositePane(target);
        return payload.Origin == source.Side.ToString() &&
            (!source.IsPlaylist || !string.IsNullOrWhiteSpace(payload.SourcePlaylist)) &&
            string.Equals(payload.SourcePlaylist, source.IsPlaylist ? source.SelectedCollection?.PlaylistName : null, StringComparison.OrdinalIgnoreCase) &&
            payload.Songs.All(Songs.Contains);
    }
    public bool CanAddToOpposite(CollectionPaneViewModel source) => CanAddBetweenPanes(OppositePane(source));
    public void DropOnPane(CollectionPaneViewModel target, SongDragPayload payload)
    {
        if (CanDropOnPane(target, payload)) AddSongs(target, payload.Songs);
    }
    public void AddToOpposite(CollectionPaneViewModel source, IReadOnlyList<SongItemViewModel> songs) => AddSongs(source == PaneA ? PaneB : PaneA, songs);
    public bool CanRemoveFromCurrentPlaylist(CollectionPaneViewModel pane) =>
        CanManagePlaylists && pane.IsPlaylist && !string.IsNullOrWhiteSpace(pane.SelectedCollection?.PlaylistName) && pane.CheckedCount > 0;
    public void RemoveFromCurrentPlaylist(CollectionPaneViewModel pane)
    {
        if (!CanRemoveFromCurrentPlaylist(pane) || pane.SelectedCollection?.PlaylistName is not string playlistName) return;
        var selected = pane.CheckedSongs.Where(song => song.PlaylistNames.Contains(playlistName, StringComparer.OrdinalIgnoreCase)).ToArray();
        try
        {
            if (IsRealDataMode && selected.Length > 0) _playlistStore!.RemoveSongs(_playlistsPath!, PlaylistFileName(playlistName), selected.Select(song => song.Identity.StableId).ToArray());
        }
        catch (Exception) { SetStatusMessage("Playlistからの削除保存に失敗しました。元データは維持されています。"); return; }
        var removed = pane.CheckedSongs.Count(song => song.RemovePlaylist(playlistName));
        SetStatusMessage($"{removed}曲を{playlistName}から削除しました（曲ファイルは保持）");
        RefreshBothPanes();
    }
    public void SetPaneFavorites(CollectionPaneViewModel pane, bool value)
    {
        SetFavorites(pane.CheckedSongs, value);
    }

    public bool CanSetBlacklist(CollectionPaneViewModel pane) => CanSetBlacklistSongs(pane.CheckedSongs);
    public bool CanRemoveBlacklist(CollectionPaneViewModel pane) => pane.SelectedCollection?.Filter == NavigationFilter.Blacklist && CanSetBlacklistSongs(pane.CheckedSongs.Where(s => s.IsBlacklisted).ToArray());
    public void RemovePaneBlacklist(CollectionPaneViewModel pane)
    {
        if (CanRemoveBlacklist(pane)) SetPaneBlacklist(pane, false, registeredOnly: true);
    }
    private bool CanSetBlacklistSongs(IReadOnlyCollection<SongItemViewModel> songs) =>
        GameState == GameAccessState.Stopped && !IsEnvironmentScanRunning &&
        (!IsRealDataMode || (_gameRoot is not null && _songFlagsStore?.IsGameStopped == true)) &&
        songs.Count > 0 &&
        songs.All(s => s.Identity.Kind == SongKind.Custom && (!IsRealDataMode || !string.IsNullOrWhiteSpace(s.FileName)));

    public void AddPaneBlacklist(CollectionPaneViewModel pane)
    {
        if (pane.SelectedCollection?.Filter != NavigationFilter.Blacklist && CanSetBlacklist(pane))
            SetPaneBlacklist(pane, true);
    }

    private void SetPaneBlacklist(CollectionPaneViewModel pane, bool enabled, bool registeredOnly = false)
    {
        var selected = registeredOnly ? pane.CheckedSongs.Where(s => s.IsBlacklisted).ToArray() : pane.CheckedSongs;
        if (!CanSetBlacklistSongs(selected)) return;
        try
        {
            if (IsRealDataMode)
            {
                var saved = _songFlagsStore!.SetBlacklist(_gameRoot!, selected.Select(s => s.FileName!).ToArray(), enabled);
                foreach (var song in Songs) song.IsBlacklisted = song.FileName is not null && saved.Contains(song.FileName);
            }
            else
            {
                foreach (var song in selected) song.IsBlacklisted = enabled;
            }
            RefreshBothPanes();
            SetStatusMessage(UiText("BlacklistSaved"));
        }
        catch (Exception)
        {
            SetStatusMessage(UiText("BlacklistSaveFailed"));
        }
    }

    private void AddSongs(CollectionPaneViewModel target, IReadOnlyList<SongItemViewModel> songs)
    {
        if (!CanAddBetweenPanes(target) || target.SelectedCollection?.PlaylistName is not string name) return; var added = 0; var duplicates = 0;
        var additions = songs.Where(song => !song.PlaylistNames.Contains(name, StringComparer.OrdinalIgnoreCase)).ToArray();
        try
        {
            if (IsRealDataMode && additions.Length > 0) _playlistStore!.AddSongs(_playlistsPath!, PlaylistFileName(name), additions.Select(ToPlaylistWriteSong).ToArray());
        }
        catch (Exception) { SetStatusMessage("Playlistへの保存に失敗しました。元データは維持されています。"); return; }
        foreach (var song in songs) { if (song.AddPlaylist(name)) added++; else duplicates++; }
        SetStatusMessage($"{added}曲を{name}へ追加しました · {duplicates}曲は既に登録済みです"); RefreshBothPanes();
    }
    private void ApplyPaneCollection(CollectionPaneViewModel pane, NavigationItemViewModel? value)
    {
        if (pane == PaneA) { if (SetProperty(ref _selectedSourceNavigation, value, nameof(SelectedSourceNavigation))) { Changed(nameof(SelectedNavigation), nameof(SourceViewTitle), nameof(CurrentViewTitle)); RefreshSourceFilter(); } }
        else { if (SetProperty(ref _selectedDestinationNavigation, value, nameof(SelectedDestinationNavigation))) { Changed(nameof(DestinationPlaylistName)); RefreshDestination(); } }
        RaiseDestinationState(); PaneA.RaiseCommands(); PaneB.RaiseCommands();
    }
    private void RefreshBothPanes() { UpdatePlaylistCounts(); PaneA.Refresh(); PaneB.Refresh(); RefreshDestination(); RefreshSourceFilter(); }

    private bool FilterSourceSong(object item)
    {
        if (item is not SongItemViewModel s) return false;
        if (SelectedSourceNavigation?.Filter != NavigationFilter.Blacklist && s.IsBlacklisted) return false;
        if (!string.IsNullOrWhiteSpace(SearchText) && !s.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) && !s.Artist.Contains(SearchText, StringComparison.OrdinalIgnoreCase) && !s.Mapper.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) return false;
        return SelectedSourceNavigation?.Filter switch { NavigationFilter.Blacklist => s.IsBlacklisted, NavigationFilter.Favorites => s.IsFavorite, NavigationFilter.UnsortedFavorites => s.IsFavorite && s.PlaylistNames.Count == 0, NavigationFilter.AssignedFavorites => s.IsFavorite && s.PlaylistNames.Count > 0, NavigationFilter.Unassigned => s.PlaylistNames.Count == 0, NavigationFilter.MultiplePlaylists => s.PlaylistNames.Count > 1, NavigationFilter.Custom => s.Identity.Kind == SongKind.Custom, NavigationFilter.OfficialOrDlc => s.Identity.Kind == SongKind.OfficialOrDlc, NavigationFilter.RecentlyAdded => s.AddedAt >= DateTimeOffset.Now.AddDays(-30), NavigationFilter.Playlist => SelectedSourceNavigation.PlaylistName is not null && s.PlaylistNames.Contains(SelectedSourceNavigation.PlaylistName), _ => true };
    }
    private bool CanAcceptPayload(SongDragPayload p) => p.Count > 0 && CanEditDestination;
    private void AddPayloadToDestination(SongDragPayload payload)
    {
        var name = SelectedDestinationNavigation?.PlaylistName; if (name is null || IsReadOnly || IsSamePlaylist) return;
        var added = 0; var duplicates = 0;
        foreach (var song in payload.Songs) { if (!song.AddPlaylist(name)) { duplicates++; continue; } DestinationSongs.Add(song); added++; }
        SetStatusMessage($"{added}曲を{name}へ追加しました · {duplicates}曲は既に登録済みです"); RefreshMemberships();
    }
    private void SetBulkFavorite(bool value)
    {
        SetFavorites(SourceCheckedSongs.Concat(DestinationCheckedSongs).Distinct().ToArray(), value);
    }
    public bool CanEditFavorite(SongItemViewModel song) => GameState == GameAccessState.Stopped && !IsEnvironmentScanRunning &&
        (!IsRealDataMode || (_favoritesPath is not null && _songFlagsStore?.IsGameStopped == true &&
        song.Identity.Kind == SongKind.Custom && song.FavoriteReference is not null));
    public bool CanSetFavorites(IReadOnlyCollection<SongItemViewModel> songs) => songs.Count > 0 && songs.All(CanEditFavorite);
    private void ToggleFavorite(SongItemViewModel song) => SetFavorites([song], !song.IsFavorite);
    private void SetFavorites(IReadOnlyCollection<SongItemViewModel> selected, bool enabled)
    {
        if (!CanSetFavorites(selected)) return;
        try
        {
            if (IsRealDataMode)
            {
                var saved = _songFlagsStore!.SetFavorites(_favoritesPath!, selected.Select(s => s.FavoriteReference!).ToArray(), enabled);
                foreach (var song in Songs.Where(s => s.Identity.Kind == SongKind.Custom))
                    song.IsFavorite = saved.Any(e => e.EndsWith("-" + song.Identity.StableId, StringComparison.OrdinalIgnoreCase));
            }
            else { foreach (var song in selected) song.IsFavorite = enabled; }
            Changed(nameof(FavoriteCount));
            RefreshBothPanes();
            SetStatusMessage(UiText("FavoriteSaved"));
        }
        catch (Exception) { SetStatusMessage(UiText("FavoriteSaveFailed")); }
    }
    private void SetVisibleSourceChecks(bool value) => SetChecks(_sourceSongsView.Cast<SongItemViewModel>().ToArray(), true, value);
    private void SetDestinationChecks(bool value) => SetChecks(DestinationSongs, false, value);
    private void SetChecks(IEnumerable<SongItemViewModel> songs, bool source, bool value)
    {
        _suppressLegacySelectionUpdate = true;
        try { foreach (var song in songs) { if (source) song.IsSourceChecked = value; else song.IsDestinationChecked = value; } }
        finally { _suppressLegacySelectionUpdate = false; }
        RaiseSelectionState();
    }
    private static bool? CheckState(IEnumerable<SongItemViewModel> songs, bool source) { var rows = songs.ToArray(); if (rows.Length == 0) return false; var count = rows.Count(x => source ? x.IsSourceChecked : x.IsDestinationChecked); return count == 0 ? false : count == rows.Length ? true : null; }

    private void CreatePlaylist()
    {
        var name = PlaylistNameDraft.Trim(); if (!CanManagePlaylists || !IsValidNewPlaylistName) return;
        PlaylistFileReference file;
        try { file = IsRealDataMode ? _playlistStore!.Create(_playlistsPath!, name) : new($"mock-{Guid.NewGuid():N}", name); }
        catch (Exception) { SetStatusMessage("Playlistを作成できませんでした。名前と保存先を確認してください。"); return; }
        var item = new NavigationItemViewModel(file.Name, NavigationFilter.Playlist, file.Name, 0);
        ConfigurePlaylistCommands(item); PlaylistNavigation.Add(item); Playlists.Add(new(file.FileName, file.Name, 0)); _playlistFilesByName[file.Name] = file.FileName;
        SelectedDestinationNavigation = item; PlaylistNameDraft = ""; Changed(nameof(PlaylistCount)); SetStatusMessage($"Playlist「{file.Name}」を作成しました");
    }
    private void RenameSelectedPlaylist()
    {
        var item = SelectedDestinationNavigation; if (!CanManagePlaylists || item?.PlaylistName is null || !IsValidRename) return; var old = item.PlaylistName; var name = PlaylistNameDraft.Trim(); if (old == name) return;
        PlaylistFileReference file;
        try { file = IsRealDataMode ? _playlistStore!.Rename(_playlistsPath!, PlaylistFileName(old), name) : new(PlaylistFileName(old), name); }
        catch (Exception) { SetStatusMessage("Playlist名を変更できませんでした。既存名との衝突を確認してください。"); return; }
        foreach (var song in Songs) song.RenamePlaylist(old, file.Name); var i = Playlists.ToList().FindIndex(x => x.Name == old); if (i >= 0) Playlists[i] = Playlists[i] with { Id = file.FileName, Name = file.Name };
        _playlistFilesByName.Remove(old); _playlistFilesByName[file.Name] = file.FileName;
        item.Label = file.Name; item.PlaylistName = file.Name; PlaylistNameDraft = file.Name; Changed(nameof(DestinationPlaylistName)); RefreshBothPanes(); SetStatusMessage($"{old} を {file.Name} へ名前変更しました");
    }
    private void ConfirmDeletePlaylist()
    {
        var item = PendingDeletePlaylist; if (!CanManagePlaylists || item?.PlaylistName is null) return; var name = item.PlaylistName;
        try { if (IsRealDataMode) _playlistStore!.Delete(_playlistsPath!, PlaylistFileName(name)); }
        catch (Exception) { PendingDeletePlaylist = null; SetStatusMessage("Playlistを削除できませんでした。元データは維持されています。"); return; }
        foreach (var song in Songs) song.RemovePlaylist(name); PlaylistNavigation.Remove(item); var summary = Playlists.FirstOrDefault(x => x.Name == name); if (summary is not null) Playlists.Remove(summary);
        _playlistFilesByName.Remove(name);
        if (SelectedSourceNavigation == item) SelectedSourceNavigation = SmartNavigation[0];
        if (SelectedDestinationNavigation == item) SelectedDestinationNavigation = PlaylistNavigation.FirstOrDefault() ?? SmartNavigation[0];
        PendingDeletePlaylist = null; Changed(nameof(PlaylistCount)); RefreshBothPanes(); SetStatusMessage($"{name} を削除しました");
    }
    private void ConfigurePlaylistCommands(NavigationItemViewModel item)
    {
        item.OpenAsSourceCommand = new RelayCommand(() => SelectedSourceNavigation = item);
        item.SetAsDestinationCommand = new RelayCommand(() => SelectedDestinationNavigation = item);
        item.PrepareRenameCommand = new RelayCommand(() => { SelectedDestinationNavigation = item; PlaylistNameDraft = item.PlaylistName ?? item.Label; SetStatusMessage("名前欄を編集し［名前変更］を押してください"); });
        item.RequestDeleteCommand = new RelayCommand(() => RequestDeletePlaylistCommand.Execute(item), () => RequestDeletePlaylistCommand.CanExecute(item));
    }
    private void RefreshSourceFilter() { _sourceSongsView.Refresh(); Changed(nameof(VisibleSourceSongCount), nameof(VisibleSongCount)); RaiseSelectionState(); }
    private void RefreshDestination() { DestinationSongs.Clear(); var name = SelectedDestinationNavigation?.PlaylistName; if (name is not null) foreach (var song in Songs.Where(x => x.PlaylistNames.Contains(name))) DestinationSongs.Add(song); Changed(nameof(DestinationSongCount)); RaiseSelectionState(); }
    private void RefreshMemberships() { UpdatePlaylistCounts(); Changed(nameof(DestinationSongCount)); RefreshSourceFilter(); }
    private void UpdatePlaylistCounts() { foreach (var item in PlaylistNavigation) item.Count = Songs.Count(x => item.PlaylistName is not null && x.PlaylistNames.Contains(item.PlaylistName)); for (var i = 0; i < Playlists.Count; i++) Playlists[i] = Playlists[i] with { SongCount = Songs.Count(x => x.PlaylistNames.Contains(Playlists[i].Name)) }; Changed(nameof(PlaylistCount)); }
    private string PlaylistFileName(string playlistName) => _playlistFilesByName.TryGetValue(playlistName, out var fileName) ? fileName : throw new InvalidDataException("Playlist file identity is unavailable.");
    private static PlaylistWriteSong ToPlaylistWriteSong(SongItemViewModel song) => new(song.Identity.StableId, song.Title, song.Artist, song.Mapper, song.Duration?.TotalSeconds ?? 0);
    private static bool IsSafePlaylistName(string value)
    {
        var name = value.Trim();
        return name.Length is > 0 and <= 80 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !name.Any(char.IsControl);
    }
    internal void SetPaneChecked(PaneSide side, SongItemViewModel song, bool value)
    {
        _suppressLegacySelectionUpdate = true;
        try
        {
            if (side == PaneSide.A) song.IsSourceChecked = value;
            else song.IsDestinationChecked = value;
        }
        finally { _suppressLegacySelectionUpdate = false; }
    }
    private void OnSongPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressLegacySelectionUpdate) return;
        if (e.PropertyName == nameof(SongItemViewModel.IsSourceChecked)) PaneA.RaiseSelectionChanged();
        else if (e.PropertyName == nameof(SongItemViewModel.IsDestinationChecked)) PaneB.RaiseSelectionChanged();
        else return;
        RaiseSelectionState();
    }
    private void RaiseSelectionState() { Changed(nameof(SourceSelectedCount), nameof(VisibleSourceSelectedCount), nameof(DestinationSelectedCount), nameof(SourceSelectionDisplay), nameof(DestinationSelectionDisplay), nameof(HasSourceSelection), nameof(HasDestinationSelection), nameof(AreAllVisibleSourceChecked), nameof(AreAllDestinationChecked), nameof(CanBulkAdd), nameof(CanBulkFavorite)); RaiseCommandStates(); }
    private void RaiseDestinationState() { Changed(nameof(IsSamePlaylist), nameof(SamePlaylistDisplay), nameof(CanEditDestination), nameof(CanBulkAdd)); RaiseCommandStates(); }
    private void RaiseCommandStates() { _toggleFavorite.RaiseCanExecuteChanged(); _dropSongs.RaiseCanExecuteChanged(); _bulkAdd.RaiseCanExecuteChanged(); _favoriteOn.RaiseCanExecuteChanged(); _favoriteOff.RaiseCanExecuteChanged(); _createPlaylist.RaiseCanExecuteChanged(); _renamePlaylist.RaiseCanExecuteChanged(); _requestDelete.RaiseCanExecuteChanged(); _confirmDelete.RaiseCanExecuteChanged(); }
    private void ToggleGameState() => GameState = GameState == GameAccessState.RunningReadOnly ? GameAccessState.Stopped : GameAccessState.RunningReadOnly;
    private void ToggleLanguage()
    {
        LocalizationService.Toggle();
        foreach (var item in FavoritesNavigation.Concat(SmartNavigation)) item.Label = LocalizedNavigationLabel(item.Filter);
        Changed(nameof(GameStateDisplay), nameof(ChangeStateDisplay), nameof(SelectedSongTitleDisplay), nameof(PreviewAvailabilityText), nameof(SourceSelectionDisplay), nameof(DestinationSelectionDisplay));
        PaneA.RefreshLocalizedText(); PaneB.RefreshLocalizedText();
        SetStatusMessage(UiText("State.LanguageChanged"));
    }
    private static string LocalizedNavigationLabel(NavigationFilter filter) => UiText(filter switch
    {
        NavigationFilter.Favorites => "Collection.AllFavorites", NavigationFilter.UnsortedFavorites => "Collection.Unsorted",
        NavigationFilter.AssignedFavorites => "Collection.InPlaylists", NavigationFilter.AllSongs => "Collection.AllSongs",
        NavigationFilter.Unassigned => "Collection.NotInPlaylist", NavigationFilter.MultiplePlaylists => "Collection.MultiplePlaylists",
        NavigationFilter.Custom => "Collection.Custom", NavigationFilter.OfficialOrDlc => "Collection.OfficialDlc",
        NavigationFilter.RecentlyAdded => "Collection.RecentlyAdded", NavigationFilter.Blacklist => "Collection.Blacklist", _ => "Collection.Playlist"
    });
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
            InstallSummary = "環境検出中にアクセスエラーが発生しました。ゲームデータは変更していません。";
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
            _favoritesPath = environment.Find(DataLocationKind.Favorites) is { Status: DataLocationStatus.Available } favoriteLocation ? favoriteLocation.ResolvedPath : null;
            var playlistsLocation = environment.Find(DataLocationKind.Playlists);
            _playlistsPath = playlistsLocation is { ResolvedPath: not null } &&
                playlistsLocation.Status is DataLocationStatus.Available or DataLocationStatus.Unknown &&
                Directory.Exists(playlistsLocation.ResolvedPath) ? playlistsLocation.ResolvedPath : null;
            _gameRoot = environment.Find(DataLocationKind.GameRoot) is { Status: DataLocationStatus.Available } location ? location.ResolvedPath : null;
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
            _playlistFilesByName.Clear();
            foreach (var playlist in snapshot.Playlists)
            {
                Playlists.Add(playlist);
                _playlistFilesByName[playlist.Name] = playlist.Id;
                var navigation = new NavigationItemViewModel(playlist.Name, NavigationFilter.Playlist, playlist.Name, playlist.SongCount);
                ConfigurePlaylistCommands(navigation);
                PlaylistNavigation.Add(navigation);
            }
            IsRealDataMode = true;
            UpdateGameState();
            PaneA.SelectedCollection = SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs);
            PaneB.SelectedCollection = PlaylistNavigation.FirstOrDefault() ?? SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs);
            SelectedSong = Songs.FirstOrDefault();
            RefreshBothPanes();
            Changed(nameof(LibraryCount), nameof(FavoriteCount), nameof(PlaylistCount));
            var d = snapshot.Diagnostics;
            InstallSummary = $"実データ接続済み · {(_songFlagsStore is null ? "読み取り専用" : UiText("BlacklistOnly"))} · {d.TotalSongs}曲 / Favorite {d.FavoritesResolved}件 / Playlist {snapshot.Playlists.Count}件";
            SetStatusMessage(snapshot.Warnings.Count == 0 ? InstallSummary : $"{InstallSummary} · Warning {snapshot.Warnings.Count}件（データ変更なし）");
        }
        catch (Exception)
        {
            IsRealDataMode = false;
            InstallSummary = "実Libraryを安全に読み込めませんでした。ゲームデータは変更していません。";
            SetStatusMessage(InstallSummary);
        }
    }
    internal static string UiText(string key) => System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
    private void SetStatusMessage(string value) => StatusMessage = value;
    private void Changed(params string[] names) { foreach (var name in names) OnPropertyChanged(name); }
    public void Dispose()
    {
        _previewTimer.Stop();
        _gameStateTimer.Stop();
        _audioPreviewPlayer.PlaybackStateChanged -= OnPlaybackStateChanged;
        _audioPreviewPlayer.Dispose();
    }
}
