using System.Windows.Controls;
using System.Windows.Input;
using SynthRidersPlaylistManager.App.ViewModels;

namespace SynthRidersPlaylistManager.App.Views;

public partial class CollectionBrowserView : UserControl
{
    public CollectionBrowserView() => InitializeComponent();

    private void SongsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => ActivatePane();

    private void SongsGrid_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ActivatePane();

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
            DataContext is not CollectionPaneViewModel pane ||
            !pane.ToggleFavoriteCommand.CanExecute(song)) return;

        pane.Activate();
        pane.ToggleFavoriteCommand.Execute(song);
        e.Handled = true;
    }

    private void ActivatePane()
    {
        if (DataContext is CollectionPaneViewModel pane) pane.Activate();
    }
}
