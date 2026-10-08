namespace SynthRidersPlaylistManager.App.ViewModels;

using System.Windows.Input;

public enum NavigationFilter
{
    Favorites,
    UnsortedFavorites,
    AllSongs,
    Unassigned,
    Blacklist,
    Playlist
}

public sealed class NavigationItemViewModel : Mvvm.ObservableObject
{
    private string _label;
    private int? _count;
    private int _blacklistCount;

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
    public int BlacklistCount
    {
        get => _blacklistCount;
        set { if (SetProperty(ref _blacklistCount, value)) OnPropertyChanged(nameof(DisplayLabel)); }
    }
    public string DisplayLabel => Count is null
        ? Label
        : BlacklistCount == 0 ? $"{Label}  {Count}" : $"{Label}  {Count} + BL {BlacklistCount}";
    public ICommand? OpenAsSourceCommand { get; set; }
    public ICommand? SetAsDestinationCommand { get; set; }
    public ICommand? PrepareRenameCommand { get; set; }
}
