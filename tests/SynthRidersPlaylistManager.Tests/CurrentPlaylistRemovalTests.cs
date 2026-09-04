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
            using var vm = new MainViewModel();
            var pane = side == PaneSide.A ? vm.PaneA : vm.PaneB;
            var other = side == PaneSide.A ? vm.PaneB : vm.PaneA;
            var current = "remove-current-" + side;
            var retained = "remove-retained-" + side;
            var currentNavigation = new NavigationItemViewModel("Current", NavigationFilter.Playlist, current, 2);
            vm.PlaylistNavigation.Add(currentNavigation);
            var songs = vm.Songs.Take(2).ToArray();
            foreach (var song in songs) { song.AddPlaylist(current); song.AddPlaylist(retained); }
            songs[0].IsFavorite = true;
            songs[1].IsBlacklisted = true;
            var favoriteBefore = songs.Select(s => s.IsFavorite).ToArray();
            var blacklistBefore = songs.Select(s => s.IsBlacklisted).ToArray();
            pane.SelectedCollection = new("Current", NavigationFilter.Playlist, current);
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
            pane.SelectedCollection = new("Current", NavigationFilter.Playlist, current);
            Assert.False(pane.RemoveFromCurrentPlaylistCommand.CanExecute(null));

            vm.GameState = GameAccessState.Stopped;
            pane.SelectedCollection = new("All", NavigationFilter.AllSongs);
            other.SelectedCollection = new("Copy target", NavigationFilter.Playlist, "copy-target-" + side);
            var payload = pane.CreateDragPayload(songs[0]);
            Assert.True(other.DropCommand.CanExecute(payload));
            other.DropCommand.Execute(payload);
            Assert.Contains("copy-target-" + side, songs[0].PlaylistNames);
            Assert.Contains(retained, songs[0].PlaylistNames);
        }
    }
}
