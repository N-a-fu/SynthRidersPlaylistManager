using System.Collections.Specialized;
using SynthRidersPlaylistManager.App.ViewModels;

namespace SynthRidersPlaylistManager.Tests;

public sealed class CheckboxPerformanceTests
{
    [Fact]
    public void CheckboxChangesUpdateOnlyPaneSelectionStateWithoutRefreshingTheView()
    {
        var vm = new MainViewModel();
        var pane = vm.PaneA;
        var resets = 0;
        ((INotifyCollectionChanged)pane.VisibleSongs).CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) resets++;
        };

        pane.ToggleCheckedCommand.Execute(vm.Songs[0]);
        pane.ToggleCheckedCommand.Execute(vm.Songs[1]);

        Assert.Equal(2, pane.CheckedCount);
        Assert.Equal(0, vm.PaneB.CheckedCount);
        Assert.Equal(0, resets);

        pane.ClearSelectionCommand.Execute(null);

        Assert.Equal(0, pane.CheckedCount);
        Assert.Equal(0, resets);
    }

    [Fact]
    public void PaneChecksRemainIndependentAndCreateCheckedDragPayloads()
    {
        var vm = new MainViewModel();
        vm.PaneA.ToggleCheckedCommand.Execute(vm.Songs[0]);
        vm.PaneA.ToggleCheckedCommand.Execute(vm.Songs[1]);
        vm.PaneB.ToggleCheckedCommand.Execute(vm.Songs[2]);

        Assert.Equal(2, vm.PaneA.CreateDragPayload(vm.Songs[0]).Count);
        Assert.Equal(1, vm.PaneB.CreateDragPayload(vm.Songs[2]).Count);
        Assert.Equal(2, vm.PaneA.CheckedCount);
        Assert.Equal(1, vm.PaneB.CheckedCount);
    }
}
