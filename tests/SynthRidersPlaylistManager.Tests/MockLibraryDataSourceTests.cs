using SynthRidersPlaylistManager.App.Services;

namespace SynthRidersPlaylistManager.Tests;

public sealed class MockLibraryDataSourceTests
{
    [Fact]
    public void ProvidesExplicitArtificialSongsAndPlaylists()
    {
        var source = new MockLibraryDataSource();

        var songs = source.GetSongs();
        var playlists = source.GetPlaylists();

        Assert.Equal(2, songs.Count);
        Assert.All(songs, song => Assert.StartsWith("Test Song", song.Title));
        Assert.Equal(2, playlists.Count);
        Assert.All(playlists, playlist => Assert.StartsWith("Test Playlist", playlist.Name));
    }
}
