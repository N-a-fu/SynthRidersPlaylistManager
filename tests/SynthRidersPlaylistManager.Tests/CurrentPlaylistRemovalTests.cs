using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Tests;

public sealed class CurrentPlaylistRemovalTests
{
    [Fact]
    public void RemovesCheckedSongsFromEachCurrentPlaylistOnlyAndKeepsDropCopyWorking()
    {
        foreach (var side in new[] { PaneSide.A, PaneSide.B })
        {
        using var vm = ProductionViewModelFixture.Create();
            var pane = side == PaneSide.A ? vm.PaneA : vm.PaneB;
            var other = side == PaneSide.A ? vm.PaneB : vm.PaneA;
            var current = side == PaneSide.A ? "Test Playlist A" : "Test Playlist B";
            var retained = "remove-retained-" + side;
            var currentNavigation = vm.PlaylistNavigation.First(item => item.PlaylistName == current);
            var songs = vm.Songs.Where(song => song.PlaylistNames.Contains(current)).Take(2).ToArray();
            foreach (var song in songs) song.AddPlaylist(retained);
            songs[0].IsFavorite = true;
            songs[1].IsBlacklisted = true;
            var favoriteBefore = songs.Select(s => s.IsFavorite).ToArray();
            var blacklistBefore = songs.Select(s => s.IsBlacklisted).ToArray();
            pane.SelectedCollection = currentNavigation;
            foreach (var song in songs) pane.ToggleCheckedCommand.Execute(song);

            Assert.True(pane.RemoveFromCurrentPlaylistCommand.CanExecute(null));
            pane.RemoveFromCurrentPlaylistCommand.Execute(null);
            Assert.All(songs, song => Assert.DoesNotContain(current, song.PlaylistNames));
            Assert.All(songs, song => Assert.Contains(retained, song.PlaylistNames));
            Assert.Equal(0, currentNavigation.Count);
            Assert.Equal(favoriteBefore, songs.Select(s => s.IsFavorite));
            Assert.Equal(blacklistBefore, songs.Select(s => s.IsBlacklisted));
            Assert.Empty(pane.VisibleSongs.Cast<SongItemViewModel>());

            pane.SelectedCollection = new("All", NavigationFilter.AllSongs);
            Assert.False(pane.RemoveFromCurrentPlaylistCommand.CanExecute(null));
            vm.GameState = GameAccessState.RunningReadOnly;
            pane.SelectedCollection = currentNavigation;
            Assert.False(pane.RemoveFromCurrentPlaylistCommand.CanExecute(null));

            vm.GameState = GameAccessState.Stopped;
            pane.SelectedCollection = new("All", NavigationFilter.AllSongs);
            other.SelectedCollection = vm.PlaylistNavigation.First(item => item.PlaylistName == "Empty Playlist");
            var payload = pane.CreateDragPayload(songs[0]);
            Assert.True(other.DropCommand.CanExecute(payload));
            other.DropCommand.Execute(payload);
            Assert.Contains("Empty Playlist", songs[0].PlaylistNames);
            Assert.Contains(retained, songs[0].PlaylistNames);
        }
    }
}
