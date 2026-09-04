using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.App.Views;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Tests;

public sealed class RealModeDropEnablementTests
{
    [Fact]
    public void RealModeBothViewsAllowOnlyStoppedDistinctPlaylistDropsWithoutEnablingMove()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                // Synthetic library only; no game discovery or file I/O services.
                using var vm = new MainViewModel();
                typeof(MainViewModel).GetProperty(nameof(MainViewModel.IsRealDataMode))!.SetValue(vm, true);
                var resources = new ResourceDictionary { Source = new Uri("/SynthRidersPlaylistManager.App;component/Resources/Theme.xaml", UriKind.Relative) };
                var views = new[] { vm.PaneA, vm.PaneB }.Select(p => new CollectionBrowserView(resources) { DataContext = p }).ToArray();
                foreach (var view in views)
                {
                    view.Measure(new Size(900, 700)); view.Arrange(new Rect(0, 0, 900, 700)); view.UpdateLayout();
                }
                var grids = views.Select(v => (DataGrid)v.FindName("SongsGrid")).ToArray();
                foreach (var grid in grids) Assert.True(grid.AllowDrop);
                foreach (var source in new[] { vm.PaneA, vm.PaneB })
                {
                    var target = source == vm.PaneA ? vm.PaneB : vm.PaneA;
                    var sourceName = "enable-source-" + source.Side;
                    var targetName = "enable-target-" + source.Side;
                    source.SelectedCollection = new("Source", NavigationFilter.Playlist, sourceName);
                    target.SelectedCollection = new("Target", NavigationFilter.Playlist, targetName);
                    var song = vm.Songs.First();
                    song.AddPlaylist(sourceName);
                    var payload = source.CreateDragPayload(song);
                    Assert.True(target.DropCommand.CanExecute(payload));
                    target.DropCommand.Execute(payload);
                    Assert.Contains(sourceName, song.PlaylistNames);
                    Assert.Contains(targetName, song.PlaylistNames);
                    Assert.True(vm.IsReadOnly);
                    target.SelectedCollection = source.SelectedCollection;
                    Assert.False(target.DropCommand.CanExecute(payload));
                    target.SelectedCollection = new("All", NavigationFilter.AllSongs);
                    Assert.False(target.DropCommand.CanExecute(payload));
                    target.SelectedCollection = new("Target", NavigationFilter.Playlist, targetName);
                    source.SelectedCollection = new("All", NavigationFilter.AllSongs);
                    Assert.True(target.DropCommand.CanExecute(source.CreateDragPayload(song)));
                    source.SelectedCollection = new("Source", NavigationFilter.Playlist, sourceName);
                    vm.GameState = GameAccessState.RunningReadOnly;
                    foreach (var grid in grids) Assert.False(grid.AllowDrop);
                    Assert.False(target.DropCommand.CanExecute(payload));
                    vm.GameState = GameAccessState.Stopped;
                    foreach (var grid in grids) Assert.True(grid.AllowDrop);
                    Assert.True(target.DropCommand.CanExecute(payload));
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
