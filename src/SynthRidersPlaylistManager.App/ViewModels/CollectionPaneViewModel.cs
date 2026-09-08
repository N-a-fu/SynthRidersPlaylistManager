using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using SynthRidersPlaylistManager.App.Mvvm;

namespace SynthRidersPlaylistManager.App.ViewModels;

public enum PaneSide { A, B }

public enum SongSortColumn { Title, Artist, Mapper }

public sealed class CollectionPaneViewModel : ObservableObject
{
    private readonly MainViewModel _owner;
    private readonly ICollectionView _songsView;
    private NavigationItemViewModel? _selectedCollection;
    private string _searchText = "";
    private string _playlistNameDraft = "";
    private bool _isNavigatorOpen;
    private SongSortColumn? _sortColumn;
    private ListSortDirection? _sortDirection;
    private SongItemViewModel[] _checkedSongs = [];
    private int _visibleCheckedCount;
    private bool? _areAllVisibleChecked;

    public CollectionPaneViewModel(MainViewModel owner, PaneSide side)
    {
        _owner = owner; Side = side;
        _songsView = new ListCollectionView(owner.Songs);
        _songsView.Filter = FilterSong;
        SelectAllVisibleCommand = new RelayCommand(() => SetVisibleChecks(true));
        ToggleCheckedCommand = new RelayCommand<SongItemViewModel>(song => { SetChecked(song, !IsSongChecked(song)); RaiseSelectionChanged(); });
        SelectCollectionCommand = new RelayCommand<NavigationItemViewModel>(item => { SelectedCollection = item; IsNavigatorOpen = false; });
        ClearSelectionCommand = new RelayCommand(() => { foreach (var song in owner.Songs) SetChecked(song, false); RaiseSelectionChanged(); });
        AddToOppositeCommand = new RelayCommand(() => owner.AddToOpposite(this, CheckedSongs), () => owner.CanAddToOpposite(this) && CheckedCount > 0);
        RemoveFromCurrentPlaylistCommand = new RelayCommand(() => owner.RemoveFromCurrentPlaylist(this), () => owner.CanRemoveFromCurrentPlaylist(this));
        DropCommand = new RelayCommand<SongDragPayload>(payload => owner.DropOnPane(this, payload), payload => owner.CanDropOnPane(this, payload));
        FavoriteOnCommand = new RelayCommand(() => owner.SetPaneFavorites(this, true), () => owner.CanSetFavorites(CheckedSongs));
        FavoriteOffCommand = new RelayCommand(() => owner.SetPaneFavorites(this, false), () => owner.CanSetFavorites(CheckedSongs));
        RemoveBlacklistCommand = new RelayCommand(() => owner.RemovePaneBlacklist(this), () => owner.CanRemoveBlacklist(this));
        AddBlacklistCommand = new RelayCommand(() => owner.AddPaneBlacklist(this), () => SelectedCollection?.Filter != NavigationFilter.Blacklist && owner.CanSetBlacklist(this));
        CreatePlaylistCommand = new RelayCommand(() => owner.CreatePlaylist(this), () => owner.CanCreatePlaylist(this));
        RenamePlaylistCommand = new RelayCommand(() => owner.RenamePlaylist(this), () => owner.CanRenamePlaylist(this));
        UpdateSelectionSnapshot();
    }

    public PaneSide Side { get; }
    public MainViewModel Owner => _owner;
    public string PaneLabel => Side == PaneSide.A ? "BROWSER PANE A" : "BROWSER PANE B";
    public string PaneAccent => Side == PaneSide.A ? "#B46CFF" : "#FF9A3D";
    public ICollectionView VisibleSongs => _songsView;
    public IEnumerable<NavigationItemViewModel> Collections => _owner.AllCollections;
    public ICommand SelectAllVisibleCommand { get; }
    public ICommand ToggleCheckedCommand { get; }
    public ICommand SelectCollectionCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand AddToOppositeCommand { get; }
    public ICommand RemoveFromCurrentPlaylistCommand { get; }
    public ICommand DropCommand { get; }
    public ICommand FavoriteOnCommand { get; }
    public ICommand FavoriteOffCommand { get; }
    public ICommand AddBlacklistCommand { get; }
    public ICommand RemoveBlacklistCommand { get; }
    public ICommand CreatePlaylistCommand { get; }
    public ICommand RenamePlaylistCommand { get; }
    public bool IsBlacklistRemovalView => Side == PaneSide.A && SelectedCollection?.Filter == NavigationFilter.Blacklist;

    private SongItemViewModel? _selectedSong;
    public SongItemViewModel? SelectedSong
    {
        get => _selectedSong;
        set { if (SetProperty(ref _selectedSong, value) && value is not null) _owner.SelectedSong = value; }
    }
    public ICommand ToggleFavoriteCommand => _owner.ToggleFavoriteCommand;
    public NavigationItemViewModel? SelectedCollection
    {
        get => _selectedCollection;
        set { if (SetProperty(ref _selectedCollection, value)) { Refresh(); _owner.OnPaneCollectionChanged(this); Changed(nameof(CollectionName), nameof(CollectionType), nameof(IsPlaylist), nameof(IsBlacklistRemovalView)); RaisePlaylistCommands(); } }
    }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) Refresh(); } }
    public string PlaylistNameDraft { get => _playlistNameDraft; set { if (SetProperty(ref _playlistNameDraft, value)) RaisePlaylistCommands(); } }
    public string CollectionName => SelectedCollection?.Label ?? "Collection未選択";
    public string CollectionType => MainViewModel.UiText(SelectedCollection?.Filter == NavigationFilter.Playlist ? "Nav.Playlists" : SelectedCollection?.Filter is NavigationFilter.Favorites or NavigationFilter.UnsortedFavorites or NavigationFilter.AssignedFavorites ? "Nav.Favorites" : "Nav.Filters");
    public bool IsPlaylist => SelectedCollection?.Filter == NavigationFilter.Playlist;
    public bool IsNavigatorOpen { get => _isNavigatorOpen; set => SetProperty(ref _isNavigatorOpen, value); }
    public int VisibleCount => _songsView.Cast<object>().Count();
    public SongItemViewModel[] CheckedSongs => _checkedSongs;
    public int CheckedCount => _checkedSongs.Length;
    public int VisibleCheckedCount => _visibleCheckedCount;
    public bool HasSelection => CheckedCount > 0;
    public string SelectionDisplay => string.Format(MainViewModel.UiText("Browser.Selection"), CheckedCount, VisibleCheckedCount);
    public bool? AreAllVisibleChecked { get => _areAllVisibleChecked; set { if (value.HasValue) SetVisibleChecks(value.Value); } }
    public bool CanReceiveDrop => _owner.CanReceiveDrop(this);
    public bool IsActive => _owner.ActivePaneSide == Side;
    public SongSortColumn? SortColumn => _sortColumn;
    public ListSortDirection? SortDirection => _sortDirection;

    public void CycleSort(string? propertyName)
    {
        var column = propertyName switch
        {
            nameof(SongItemViewModel.Title) => SongSortColumn.Title,
            nameof(SongItemViewModel.Artist) => SongSortColumn.Artist,
            nameof(SongItemViewModel.MapperDisplay) => SongSortColumn.Mapper,
            _ => (SongSortColumn?)null
        };
        if (column is null) return;

        if (_sortColumn != column)
        {
            _sortColumn = column;
            _sortDirection = ListSortDirection.Ascending;
        }
        else if (_sortDirection == ListSortDirection.Ascending)
            _sortDirection = ListSortDirection.Descending;
        else
        {
            _sortColumn = null;
            _sortDirection = null;
        }

        using (_songsView.DeferRefresh())
        {
            _songsView.SortDescriptions.Clear();
            if (_sortColumn is not null && _sortDirection is not null)
                _songsView.SortDescriptions.Add(new SortDescription(SortPropertyName(_sortColumn.Value), _sortDirection.Value));
        }
        Changed(nameof(SortColumn), nameof(SortDirection), nameof(VisibleCount));
    }

    private static string SortPropertyName(SongSortColumn column) => column switch
    {
        SongSortColumn.Title => nameof(SongItemViewModel.Title),
        SongSortColumn.Artist => nameof(SongItemViewModel.Artist),
        _ => nameof(SongItemViewModel.MapperDisplay)
    };

    public SongDragPayload CreateDragPayload(SongItemViewModel dragged) => new(IsChecked(dragged) ? CheckedSongs : [dragged], Side.ToString(), IsPlaylist ? SelectedCollection?.PlaylistName : null);
    public void Activate() => _owner.ActivatePane(this);
    public void RaiseActiveStateChanged() => OnPropertyChanged(nameof(IsActive));
    public void RefreshLocalizedText() => Changed(nameof(CollectionType), nameof(SelectionDisplay), nameof(CollectionName));
    public bool IsSongChecked(SongItemViewModel song) => IsChecked(song);
    public void Refresh() { _songsView.Refresh(); UpdateSelectionSnapshot(); Changed(nameof(VisibleCount), nameof(VisibleCheckedCount), nameof(SelectionDisplay), nameof(AreAllVisibleChecked)); RaiseCommands(); }
    public void RaiseSelectionChanged() { UpdateSelectionSnapshot(); Changed(nameof(CheckedCount), nameof(VisibleCheckedCount), nameof(HasSelection), nameof(SelectionDisplay), nameof(AreAllVisibleChecked)); RaiseSelectionCommands(); }
    public void RaiseCommands()
    {
        RaiseSelectionCommands();
        RaisePlaylistCommands();
        Changed(nameof(CanReceiveDrop));
        ((RelayCommand<SongDragPayload>)DropCommand).RaiseCanExecuteChanged();
    }
    private void RaiseSelectionCommands()
    {
        ((RelayCommand)AddToOppositeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RemoveFromCurrentPlaylistCommand).RaiseCanExecuteChanged();
        ((RelayCommand)FavoriteOnCommand).RaiseCanExecuteChanged(); ((RelayCommand)FavoriteOffCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddBlacklistCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RemoveBlacklistCommand).RaiseCanExecuteChanged();
    }
    private void RaisePlaylistCommands()
    {
        ((RelayCommand)CreatePlaylistCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RenamePlaylistCommand).RaiseCanExecuteChanged();
    }
    private bool FilterSong(object value)
    {
        if (value is not SongItemViewModel song) return false;
        if (SelectedCollection?.Filter != NavigationFilter.Blacklist && song.IsBlacklisted) return false;
        if (!string.IsNullOrWhiteSpace(SearchText) && !song.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) && !song.Artist.Contains(SearchText, StringComparison.OrdinalIgnoreCase) && !song.Mapper.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) return false;
        return SelectedCollection?.Filter switch { NavigationFilter.Favorites => song.IsFavorite, NavigationFilter.UnsortedFavorites => song.IsFavorite && song.PlaylistNames.Count == 0, NavigationFilter.AssignedFavorites => song.IsFavorite && song.PlaylistNames.Count > 0, NavigationFilter.Unassigned => song.PlaylistNames.Count == 0, NavigationFilter.MultiplePlaylists => song.PlaylistNames.Count > 1, NavigationFilter.Custom => song.Identity.Kind == Core.Models.SongKind.Custom, NavigationFilter.OfficialOrDlc => song.Identity.Kind == Core.Models.SongKind.OfficialOrDlc, NavigationFilter.RecentlyAdded => song.AddedAt >= DateTimeOffset.Now.AddDays(-30), NavigationFilter.Blacklist => song.IsBlacklisted, NavigationFilter.Playlist => SelectedCollection.PlaylistName is not null && song.PlaylistNames.Contains(SelectedCollection.PlaylistName), _ => true };
    }
    private bool IsChecked(SongItemViewModel song) => Side == PaneSide.A ? song.IsSourceChecked : song.IsDestinationChecked;
    private void SetChecked(SongItemViewModel song, bool value) => _owner.SetPaneChecked(Side, song, value);
    private void SetVisibleChecks(bool value) { foreach (var song in _songsView.Cast<SongItemViewModel>().ToArray()) SetChecked(song, value); RaiseSelectionChanged(); }
    private void UpdateSelectionSnapshot()
    {
        _checkedSongs = _owner.Songs.Where(IsChecked).ToArray();
        var visibleRows = _songsView.Cast<SongItemViewModel>().ToArray();
        _visibleCheckedCount = visibleRows.Count(IsChecked);
        _areAllVisibleChecked = visibleRows.Length == 0 || _visibleCheckedCount == 0
            ? false
            : _visibleCheckedCount == visibleRows.Length ? true : null;
    }
    private void Changed(params string[] names) { foreach (var name in names) OnPropertyChanged(name); }
}
