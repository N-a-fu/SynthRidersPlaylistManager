using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Tests;

public sealed class PlaylistCreateButtonStateTests
{
    [Fact]
    public void CreateAvailabilityDependsOnNameAndGameStateNotPaneSelection()
    {
        using var vm = ProductionViewModelFixture.Create();

        vm.PlaylistNameDraft = "Button State Test";
        Assert.True(vm.CreatePlaylistCommand.CanExecute(null));

        vm.PaneA.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.Custom);
        vm.PaneB.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs);
        Assert.Equal("Button State Test", vm.PlaylistNameDraft);
        Assert.True(vm.CreatePlaylistCommand.CanExecute(null));

        vm.GameState = GameAccessState.RunningReadOnly;
        Assert.False(vm.CreatePlaylistCommand.CanExecute(null));
        vm.GameState = GameAccessState.Stopped;
        Assert.True(vm.CreatePlaylistCommand.CanExecute(null));
    }
}
