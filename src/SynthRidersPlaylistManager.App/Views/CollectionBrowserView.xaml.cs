using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using SynthRidersPlaylistManager.App.ViewModels;

namespace SynthRidersPlaylistManager.App.Views;

public partial class CollectionBrowserView : UserControl
{
    public CollectionBrowserView() : this(null) { }
    internal CollectionBrowserView(System.Windows.ResourceDictionary? resources)
    {
        if (resources is not null) Resources.MergedDictionaries.Add(resources);
        InitializeComponent();
    }

    private void SongsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ActivatePane();
        if (sender is not DataGrid grid || DataContext is not CollectionPaneViewModel pane) return;
        var origin = e.OriginalSource as System.Windows.DependencyObject;
        if (!Behaviors.SongDragDropBehavior.IsInteractiveDragBlocker(origin, grid) &&
            Behaviors.SongDragDropBehavior.ResolveDragItem(grid, origin) is { } song)
            pane.Owner.SelectedSong = song;
    }

    private void SongsGrid_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ActivatePane();

    private void SongsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (DataContext is not CollectionPaneViewModel pane || !e.Column.CanUserSort) return;
        pane.CycleSort(e.Column.SortMemberPath);
        if (sender is not DataGrid grid) return;
        foreach (var column in grid.Columns) column.SortDirection = null;
        if (pane.SortDirection is { } direction) e.Column.SortDirection = direction;
    }

    private void SongCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not CheckBox { DataContext: SongItemViewModel song } ||
            DataContext is not CollectionPaneViewModel pane ||
            !pane.ToggleCheckedCommand.CanExecute(song)) return;

        pane.Activate();
        pane.ToggleCheckedCommand.Execute(song);
        e.Handled = true;
    }

    private void FavoriteStar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button { DataContext: SongItemViewModel song } ||
            DataContext is not CollectionPaneViewModel pane) return;

        e.Handled = true;
        if (!pane.ToggleFavoriteCommand.CanExecute(song)) return;

        pane.Activate();
        pane.ToggleFavoriteCommand.Execute(song);
        e.Handled = true;
    }

    private void ActivatePane()
    {
        if (DataContext is CollectionPaneViewModel pane) pane.Activate();
    }
}
