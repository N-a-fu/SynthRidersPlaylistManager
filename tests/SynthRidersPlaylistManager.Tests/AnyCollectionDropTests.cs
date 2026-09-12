using System.Runtime.ExceptionServices;
using SynthRidersPlaylistManager.App.ViewModels;

namespace SynthRidersPlaylistManager.Tests;

public sealed class AnyCollectionDropTests
{
    [Theory]
    [InlineData(NavigationFilter.AllSongs, NavigationFilter.Playlist, false, true)]
    [InlineData(NavigationFilter.Favorites, NavigationFilter.Playlist, false, true)]
    [InlineData(NavigationFilter.Playlist, NavigationFilter.Playlist, false, true)]
    [InlineData(NavigationFilter.Playlist, NavigationFilter.Playlist, true, false)]
    [InlineData(NavigationFilter.Playlist, NavigationFilter.AllSongs, false, false)]
    [InlineData(NavigationFilter.AllSongs, NavigationFilter.AllSongs, false, false)]
    public void DropAcceptsAnySourceButRequiresDistinctPlaylistDestination(
        NavigationFilter sourceFilter, NavigationFilter targetFilter, bool samePlaylist, bool expected)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var side in new[] { PaneSide.A, PaneSide.B })
                {
                    // Synthetic data; real-mode guards only, no game-file services.
        using var vm = ProductionViewModelFixture.Create();
                    typeof(MainViewModel).GetProperty(nameof(MainViewModel.IsRealDataMode))!.SetValue(vm, true);
                    var source = side == PaneSide.A ? vm.PaneA : vm.PaneB;
                    var target = side == PaneSide.A ? vm.PaneB : vm.PaneA;
                    var sourceId = sourceFilter == NavigationFilter.Playlist ? "Test Playlist A" : null;
                    var targetId = targetFilter == NavigationFilter.Playlist ? samePlaylist ? sourceId : "Test Playlist B" : null;
                    source.SelectedCollection = sourceId is null
                        ? new("Source", sourceFilter)
                        : vm.PlaylistNavigation.First(item => item.PlaylistName == sourceId);
                    target.SelectedCollection = targetId is null
                        ? new("Target", targetFilter)
                        : vm.PlaylistNavigation.First(item => item.PlaylistName == targetId);
                    var song = sourceId is null
                        ? vm.Songs.First(item => item.IsFavorite && !item.PlaylistNames.Contains("Test Playlist B"))
                        : vm.Songs.First(item => item.PlaylistNames.Contains(sourceId) && !item.PlaylistNames.Contains("Test Playlist B"));
                    var before = song.PlaylistNames.ToArray();
                    var payload = source.CreateDragPayload(song);
                    if (sourceId is null) Assert.Null(payload.SourcePlaylist);
                    Assert.Equal(expected, target.DropCommand.CanExecute(payload));
                    // Exercise the existing Add path and its guard, never a writer.
                    vm.DropOnPane(target, payload);
                    if (expected) Assert.Contains(targetId!, song.PlaylistNames);
                    else Assert.Equal(before, song.PlaylistNames);
                    foreach (var membership in before) Assert.Contains(membership, song.PlaylistNames);
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
