using System.Windows.Controls;
using System.Runtime.ExceptionServices;
using SynthRidersPlaylistManager.App.Behaviors;
using SynthRidersPlaylistManager.App.ViewModels;

namespace SynthRidersPlaylistManager.Tests;

public sealed class SongDragDropBehaviorTests
{
    [Fact]
    public void DragCandidateComesFromMouseDownRowEvenWhenGridHasNoSelection()
    {
        RunSta(() =>
        {
            var vm = new MainViewModel();
            var expected = vm.PaneB.VisibleSongs.Cast<SongItemViewModel>().First();
            var grid = new DataGrid { SelectedItem = null };
            var row = new DataGridRow { Item = expected };

            var actual = SongDragDropBehavior.ResolveDragItem(grid, row);

            Assert.Same(expected, actual);
            Assert.Null(grid.SelectedItem);
        });
    }

    [Fact]
    public void BothPaneDropTargetsEnableAllowDropAndKeepOwnCommands()
    {
        RunSta(() =>
        {
            var vm = new MainViewModel();
            var paneAGrid = new DataGrid { DataContext = vm.PaneA };
            var paneBGrid = new DataGrid { DataContext = vm.PaneB };

        SongDragDropBehavior.SetEnableDrop(paneAGrid, true);
        SongDragDropBehavior.SetDropCommand(paneAGrid, vm.PaneA.DropCommand);
        SongDragDropBehavior.SetEnableDrop(paneBGrid, true);
        SongDragDropBehavior.SetDropCommand(paneBGrid, vm.PaneB.DropCommand);

        Assert.True(paneAGrid.AllowDrop);
        Assert.True(paneBGrid.AllowDrop);
        Assert.Same(vm.PaneA.DropCommand, SongDragDropBehavior.GetDropCommand(paneAGrid));
            Assert.Same(vm.PaneB.DropCommand, SongDragDropBehavior.GetDropCommand(paneBGrid));
        });
    }

    [Fact]
    public void MouseDownPayloadUsesCheckedSongsFromActualPaneInBothDirections()
    {
        RunSta(() =>
        {
            var vm = new MainViewModel();
            vm.Songs[2].IsSourceChecked = true;
            vm.Songs[3].IsSourceChecked = true;
            vm.Songs[4].IsDestinationChecked = true;
            vm.Songs[5].IsDestinationChecked = true;
            var gridA = new DataGrid { DataContext = vm.PaneA };
            var gridB = new DataGrid { DataContext = vm.PaneB };

            var payloadA = SongDragDropBehavior.CreateDragPayloadForMouseDown(gridA, new DataGridRow { Item = vm.Songs[2] });
            var payloadB = SongDragDropBehavior.CreateDragPayloadForMouseDown(gridB, new DataGridRow { Item = vm.Songs[4] });

            Assert.NotNull(payloadA); Assert.Equal("A", payloadA.Origin); Assert.Equal(2, payloadA.Count);
            Assert.NotNull(payloadB); Assert.Equal("B", payloadB.Origin); Assert.Equal(2, payloadB.Count);
        });
    }

    [Fact]
    public void FavoriteAndCheckboxControlsDoNotStartSongDrag()
    {
        RunSta(() =>
        {
            var grid = new DataGrid();

            Assert.True(SongDragDropBehavior.IsInteractiveDragBlocker(new Button(), grid));
            Assert.True(SongDragDropBehavior.IsInteractiveDragBlocker(new CheckBox(), grid));
            Assert.False(SongDragDropBehavior.IsInteractiveDragBlocker(new DataGridRow(), grid));
        });
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception exception) { failure = exception; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
