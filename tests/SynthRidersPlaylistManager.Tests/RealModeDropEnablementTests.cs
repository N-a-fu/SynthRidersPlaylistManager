using System.Windows;
using System.Windows.Controls;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.App.Views;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Tests;

[Collection(WpfTestCollection.Name)]
public sealed class RealModeDropEnablementTests(WpfTestFixture wpf)
{
    [Fact]
    public void RealModeBothViewsAllowOnlyStoppedDistinctPlaylistDropsWithoutEnablingMove()
    {
        wpf.Run(() =>
        {
                using var vm = ProductionViewModelFixture.Create();
                var assemblyName = typeof(CollectionBrowserView).Assembly.GetName().Name;
                var resources = new ResourceDictionary { Source = new Uri($"/{assemblyName};component/Resources/Theme.xaml", UriKind.Relative) };
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
                    var sourceName = source.Side == PaneSide.A ? "Test Playlist A" : "Test Playlist B";
                    var targetName = "Empty Playlist";
                    source.SelectedCollection = vm.PlaylistNavigation.First(item => item.PlaylistName == sourceName);
                    target.SelectedCollection = vm.PlaylistNavigation.First(item => item.PlaylistName == targetName);
                    var song = vm.Songs.First(item => item.PlaylistNames.Contains(sourceName) && !item.PlaylistNames.Contains(targetName));
                    var payload = source.CreateDragPayload(song);
                    Assert.True(target.DropCommand.CanExecute(payload));
                    target.DropCommand.Execute(payload);
                    Assert.Contains(sourceName, song.PlaylistNames);
                    Assert.Contains(targetName, song.PlaylistNames);
                    target.SelectedCollection = source.SelectedCollection;
                    Assert.False(target.DropCommand.CanExecute(payload));
                    target.SelectedCollection = new("All", NavigationFilter.AllSongs);
                    Assert.False(target.DropCommand.CanExecute(payload));
                    target.SelectedCollection = vm.PlaylistNavigation.First(item => item.PlaylistName == targetName);
                    source.SelectedCollection = new("All", NavigationFilter.AllSongs);
                    Assert.True(target.DropCommand.CanExecute(source.CreateDragPayload(song)));
                    source.SelectedCollection = vm.PlaylistNavigation.First(item => item.PlaylistName == sourceName);
                    vm.GameState = GameAccessState.RunningReadOnly;
                    foreach (var grid in grids) Assert.False(grid.AllowDrop);
                    Assert.False(target.DropCommand.CanExecute(payload));
                    vm.GameState = GameAccessState.Stopped;
                    foreach (var grid in grids) Assert.True(grid.AllowDrop);
                    Assert.True(target.DropCommand.CanExecute(payload));
                }
        });
    }
}
