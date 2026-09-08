using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.App.Views;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Tests;

[Collection(WpfTestCollection.Name)]
public sealed class SongFlagsViewTests(WpfTestFixture wpf)
{
    [Fact]
    public void ProductionViewDimsOnlyInformationAndHeartDoesNotSelectOrCheck()
    {
        wpf.Run(() =>
        {
                foreach (var paneA in new[] { true, false })
                {
                using var vm = new MainViewModel(new SynthRidersPlaylistManager.App.Services.MockLibraryDataSource());
                var songs = vm.Songs.Where(s => s.Identity.Kind == SongKind.Custom).Take(2).ToArray();
                foreach (var song in songs) song.IsBlacklisted = true;
                var pane = paneA ? vm.PaneA : vm.PaneB;
                pane.SelectedCollection = vm.SmartNavigation.Single(x => x.Filter == NavigationFilter.Blacklist);
                var view = new CollectionBrowserView { DataContext = pane };
                view.Measure(new Size(900, 700)); view.Arrange(new Rect(0, 0, 900, 700)); view.UpdateLayout();
                var blacklistMenu = Assert.Single(Descendants<Button>(view), b => b.DataContext is NavigationItemViewModel { Filter: NavigationFilter.Blacklist });
                Assert.Equal(paneA ? Visibility.Visible : Visibility.Collapsed, blacklistMenu.Visibility);
                Assert.All(Descendants<Button>(view).Where(b => b.DataContext is NavigationItemViewModel { Filter: not NavigationFilter.Blacklist }),
                    b => Assert.Equal(Visibility.Visible, b.Visibility));
                var grid = (DataGrid)view.FindName("SongsGrid");
                grid.SelectedItem = songs[1];
                grid.UpdateLayout();
                var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(songs[0]);
                Assert.NotNull(row);
                Assert.True(row.IsEnabled); Assert.Equal(1, row.Opacity);
                var cells = Descendants<DataGridCell>(row).OrderBy(c => c.Column.DisplayIndex).ToArray();
                Assert.Equal(8, cells.Length);
                Assert.Equal(1, cells[0].Opacity); Assert.Equal(1, cells[1].Opacity);
                Assert.All(cells.Skip(2), c => Assert.Equal(0.55, c.Opacity));
                var check = Assert.Single(Descendants<CheckBox>(cells[0]));
                Assert.True(check.IsEnabled); Assert.Equal(1, check.Opacity);
                var heart = Assert.Single(Descendants<Button>(cells[1]));
                Assert.True(heart.IsEnabled); Assert.Equal(1, heart.Opacity);
                var favorite = songs[0].IsFavorite;
                var selected = vm.SelectedSong;
                heart.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                Assert.Equal(!favorite, songs[0].IsFavorite);
                Assert.Same(songs[1], grid.SelectedItem);
                Assert.Same(selected, vm.SelectedSong);
                Assert.False(songs[0].IsSourceChecked); Assert.False(songs[0].IsDestinationChecked);
                grid.UpdateLayout();
                row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(songs[0]);
                check = Descendants<CheckBox>(row).First();
                check.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                Assert.True(pane.IsSongChecked(songs[0]));
                Assert.Same(selected, vm.SelectedSong);
                foreach (var collection in vm.AllCollections.ToArray())
                {
                    pane.SelectedCollection = collection;
                    pane.ClearSelectionCommand.Execute(null);
                    view.UpdateLayout();
                    var action = Assert.Single(Descendants<Button>(view), b => ReferenceEquals(b.Command, pane.AddBlacklistCommand) || ReferenceEquals(b.Command, pane.RemoveBlacklistCommand));
                    Assert.Equal(paneA && collection.Filter == NavigationFilter.Blacklist ? "ブラックリスト解除" : "ブラックリスト追加", action.Content);
                    Assert.Equal(paneA, HasVisibleAncestorChain(action));
                    Assert.False(action.IsEnabled);
                    pane.ToggleCheckedCommand.Execute(songs[0]);
                    view.UpdateLayout();
                    Assert.Equal(paneA, HasVisibleAncestorChain(action));
                    Assert.Equal(paneA || collection.Filter != NavigationFilter.Blacklist, action.IsEnabled);
                }
                }
        });
    }

    // The headless View has no shown Window, so inspect effective Visibility through
    // every ancestor instead of IsVisible (which also requires a presentation source).
    private static bool HasVisibleAncestorChain(DependencyObject element)
    {
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement ui && ui.Visibility != Visibility.Visible) return false;
        return true;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T target) yield return target;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
