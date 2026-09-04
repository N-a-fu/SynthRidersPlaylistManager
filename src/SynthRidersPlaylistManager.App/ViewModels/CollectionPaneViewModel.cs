using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using SynthRidersPlaylistManager.App.Mvvm;

namespace SynthRidersPlaylistManager.App.ViewModels;

public enum PaneSide { A, B }

public sealed class CollectionPaneViewModel : ObservableObject
{
    private readonly MainViewModel _owner;
    private readonly ICollectionView _songsView;
    private NavigationItemViewModel? _selectedCollection;
    private string _searchText = "";
    private bool _isNavigatorOpen;

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
        MoveToOppositeCommand = new RelayCommand(() => owner.MoveToOpposite(this, CheckedSongs), () => owner.CanMoveToOpposite(this) && CheckedCount > 0);
        DropCommand = new RelayCommand<SongDragPayload>(payload => owner.DropOnPane(this, payload), payload => payload.Count > 0 && owner.CanReceiveDrop(this) && payload.Origin != Side.ToString());
        FavoriteOnCommand = new RelayCommand(() => owner.SetPaneFavorites(this, true), () => !owner.IsReadOnly && CheckedCount > 0);
        FavoriteOffCommand = new RelayCommand(() => owner.SetPaneFavorites(this, false), () => !owner.IsReadOnly && CheckedCount > 0);
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
    public ICommand MoveToOppositeCommand { get; }
    public ICommand DropCommand { get; }
    public ICommand FavoriteOnCommand { get; }
    public ICommand FavoriteOffCommand { get; }
    public ICommand ToggleFavoriteCommand => _owner.ToggleFavoriteCommand;
    public NavigationItemViewModel? SelectedCollection
    {
        get => _selectedCollection;
        set { if (SetProperty(ref _selectedCollection, value)) { Refresh(); _owner.OnPaneCollectionChanged(this); Changed(nameof(CollectionName), nameof(CollectionType), nameof(IsPlaylist)); } }
    }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) Refresh(); } }
    public string CollectionName => SelectedCollection?.Label ?? "Collection未選択";
    public string CollectionType => SelectedCollection?.Filter == NavigationFilter.Playlist ? "PLAYLIST" : SelectedCollection?.Filter switch { NavigationFilter.Favorites or NavigationFilter.UnsortedFavorites or NavigationFilter.AssignedFavorites => "FAVORITES", _ => "SMART FILTER" };
    public bool IsPlaylist => SelectedCollection?.Filter == NavigationFilter.Playlist;
    public bool IsNavigatorOpen { get => _isNavigatorOpen; set => SetProperty(ref _isNavigatorOpen, value); }
    public int VisibleCount => _songsView.Cast<object>().Count();
    public SongItemViewModel[] CheckedSongs => _owner.Songs.Where(IsChecked).ToArray();
    public int CheckedCount => CheckedSongs.Length;
    public int VisibleCheckedCount => _songsView.Cast<SongItemViewModel>().Count(IsChecked);
    public bool HasSelection => CheckedCount > 0;
    public string SelectionDisplay => $"{CheckedCount}曲選択中（表示中{VisibleCheckedCount}曲）";
    public bool? AreAllVisibleChecked { get { var rows = _songsView.Cast<SongItemViewModel>().ToArray(); var count = rows.Count(IsChecked); return rows.Length == 0 || count == 0 ? false : count == rows.Length ? true : null; } set { if (value.HasValue) SetVisibleChecks(value.Value); } }
    public bool CanReceiveDrop => _owner.CanReceiveDrop(this);
    public bool IsActive => _owner.ActivePaneSide == Side;

    public SongDragPayload CreateDragPayload(SongItemViewModel dragged) => new(IsChecked(dragged) ? CheckedSongs : [dragged], Side.ToString());
    public void Activate() => _owner.ActivatePane(this);
    public void RaiseActiveStateChanged() => OnPropertyChanged(nameof(IsActive));
    public bool IsSongChecked(SongItemViewModel song) => IsChecked(song);
    public void Refresh() { _songsView.Refresh(); Changed(nameof(VisibleCount), nameof(VisibleCheckedCount), nameof(SelectionDisplay), nameof(AreAllVisibleChecked)); RaiseCommands(); }
    public void RaiseSelectionChanged() { Changed(nameof(CheckedCount), nameof(VisibleCheckedCount), nameof(HasSelection), nameof(SelectionDisplay), nameof(AreAllVisibleChecked)); RaiseCommands(); }
    public void RaiseCommands()
    {
        ((RelayCommand)AddToOppositeCommand).RaiseCanExecuteChanged(); ((RelayCommand)MoveToOppositeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)FavoriteOnCommand).RaiseCanExecuteChanged(); ((RelayCommand)FavoriteOffCommand).RaiseCanExecuteChanged(); Changed(nameof(CanReceiveDrop));
        ((RelayCommand<SongDragPayload>)DropCommand).RaiseCanExecuteChanged();
    }
    private bool FilterSong(object value)
    {
        if (value is not SongItemViewModel song) return false;
        if (!string.IsNullOrWhiteSpace(SearchText) && !song.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) && !song.Artist.Contains(SearchText, StringComparison.OrdinalIgnoreCase) && !song.Mapper.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) return false;
        return SelectedCollection?.Filter switch { NavigationFilter.Favorites => song.IsFavorite, NavigationFilter.UnsortedFavorites => song.IsFavorite && song.PlaylistNames.Count == 0, NavigationFilter.AssignedFavorites => song.IsFavorite && song.PlaylistNames.Count > 0, NavigationFilter.Unassigned => song.PlaylistNames.Count == 0, NavigationFilter.MultiplePlaylists => song.PlaylistNames.Count > 1, NavigationFilter.Custom => song.Identity.Kind == Core.Models.SongKind.Custom, NavigationFilter.OfficialOrDlc => song.Identity.Kind == Core.Models.SongKind.OfficialOrDlc, NavigationFilter.RecentlyAdded => song.AddedAt >= DateTimeOffset.Now.AddDays(-30), NavigationFilter.Blacklist => false, NavigationFilter.Playlist => SelectedCollection.PlaylistName is not null && song.PlaylistNames.Contains(SelectedCollection.PlaylistName), _ => true };
    }
    private bool IsChecked(SongItemViewModel song) => Side == PaneSide.A ? song.IsSourceChecked : song.IsDestinationChecked;
    private void SetChecked(SongItemViewModel song, bool value) { if (Side == PaneSide.A) song.IsSourceChecked = value; else song.IsDestinationChecked = value; }
    private void SetVisibleChecks(bool value) { foreach (var song in _songsView.Cast<SongItemViewModel>().ToArray()) SetChecked(song, value); RaiseSelectionChanged(); }
    private void Changed(params string[] names) { foreach (var name in names) OnPropertyChanged(name); }
}
