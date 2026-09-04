namespace SynthRidersPlaylistManager.App.ViewModels;

using System.Windows.Input;

public enum NavigationFilter
{
    Favorites,
    UnsortedFavorites,
    AssignedFavorites,
    AllSongs,
    Unassigned,
    MultiplePlaylists,
    Custom,
    OfficialOrDlc,
    RecentlyAdded,
    Playlist
}

public sealed class NavigationItemViewModel : Mvvm.ObservableObject
{
    private string _label;
    private int? _count;

    public NavigationItemViewModel(string label, NavigationFilter filter, string? playlistName = null, int? count = null)
    {
        _label = label;
        Filter = filter;
        PlaylistName = playlistName;
        _count = count;
    }

    public string Label
    {
        get => _label;
        set { if (SetProperty(ref _label, value)) OnPropertyChanged(nameof(DisplayLabel)); }
    }
    public NavigationFilter Filter { get; }
    public string? PlaylistName { get; set; }
    public int? Count
    {
        get => _count;
        set { if (SetProperty(ref _count, value)) OnPropertyChanged(nameof(DisplayLabel)); }
    }
    public string DisplayLabel => Count is null ? Label : $"{Label}  {Count}";
    public ICommand? OpenAsSourceCommand { get; set; }
    public ICommand? SetAsDestinationCommand { get; set; }
    public ICommand? PrepareRenameCommand { get; set; }
    public ICommand? DuplicateCommand { get; set; }
    public ICommand? RequestDeleteCommand { get; set; }
}
