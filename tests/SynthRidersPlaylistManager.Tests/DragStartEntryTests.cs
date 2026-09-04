using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SynthRidersPlaylistManager.App.Behaviors;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.App.Views;

namespace SynthRidersPlaylistManager.Tests;

public sealed class DragStartEntryTests
{
    [Fact]
    public void RealModeBothViewsRegisterMouseDownAndBuildOnlyTheCorrectDragCandidate()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var side in new[] { PaneSide.A, PaneSide.B })
                {
                    using var vm = new MainViewModel();
                    // No real files: enable the same state that disabled the actual entry.
                    typeof(MainViewModel).GetProperty(nameof(MainViewModel.IsRealDataMode))!.SetValue(vm, true);
                    var pane = side == PaneSide.A ? vm.PaneA : vm.PaneB;
                    var other = side == PaneSide.A ? vm.PaneB : vm.PaneA;
                    var songs = vm.Songs.Take(3).ToArray();
                    foreach (var song in songs) song.AddPlaylist("entry-fixture");
                    pane.SelectedCollection = new("fixture", NavigationFilter.Playlist, "entry-fixture");
                    var resources = new ResourceDictionary { Source = new Uri("/SynthRidersPlaylistManager.App;component/Resources/Theme.xaml", UriKind.Relative) };
                    var view = new CollectionBrowserView(resources) { DataContext = pane };
                    view.Measure(new Size(900, 700)); view.Arrange(new Rect(0, 0, 900, 700)); view.UpdateLayout();
                    var grid = (DataGrid)view.FindName("SongsGrid");
                    Assert.True(SongDragDropBehavior.GetEnableDrag(grid));
                    Assert.True(SongDragDropBehavior.GetEnableDrop(grid)); // stopped: in-memory Add is allowed
                    pane.ToggleCheckedCommand.Execute(songs[0]);
                    pane.ToggleCheckedCommand.Execute(songs[1]);
                    other.ToggleCheckedCommand.Execute(songs[2]);
                    var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(songs[0]);
                    MouseDown(row);
                    var payload = Pending(grid);
                    Assert.NotNull(payload);
                    Assert.Equal(side.ToString(), payload.Origin);
                    Assert.Equal(songs.Take(2), payload.Songs);
                    MouseDown((DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(songs[2]));
                    Assert.Same(songs[2], Assert.Single(Pending(grid)!.Songs));
                    var cells = Descendants<DataGridCell>(row).OrderBy(c => c.Column.DisplayIndex).ToArray();
                    MouseDown(Descendants<CheckBox>(cells[0]).Single());
                    Assert.Null(Pending(grid));
                    MouseDown(Descendants<Button>(cells[1]).Single());
                    Assert.Null(Pending(grid));
                    Assert.True(other.IsSongChecked(songs[2]));
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void ClickOrSmallMotionDoesNotPassDragThreshold()
    {
        Assert.False(SongDragDropBehavior.ExceedsDragThreshold(new Point(), new Point()));
        Assert.False(SongDragDropBehavior.ExceedsDragThreshold(new Point(), new Point(SystemParameters.MinimumHorizontalDragDistance / 2, SystemParameters.MinimumVerticalDragDistance / 2)));
        Assert.True(SongDragDropBehavior.ExceedsDragThreshold(new Point(), new Point(SystemParameters.MinimumHorizontalDragDistance + 1, 0)));
    }
    private static void MouseDown(UIElement element) => element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
    private static SongDragPayload? Pending(DataGrid grid) => grid.GetValue((DependencyProperty)typeof(SongDragDropBehavior).GetField("DragPayloadProperty", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!) as SongDragPayload;
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) yield return result;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
