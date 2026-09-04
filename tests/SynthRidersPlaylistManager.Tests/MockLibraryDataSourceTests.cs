using SynthRidersPlaylistManager.App.Services;

namespace SynthRidersPlaylistManager.Tests;

public sealed class MockLibraryDataSourceTests
{
    [Fact]
    public void ProvidesThirtySongsAndExpectedPlaylists()
    {
        var source = new MockLibraryDataSource();

        var songs = source.GetSongs();
        var playlists = source.GetPlaylists();

        Assert.Equal(30, songs.Count);
        Assert.Contains(songs, song => song.Title == "TEST OFFICIAL SONG");
        Assert.Contains(songs, song => song.Title == "TEST CUSTOM SONG" && song.Mapper == "Test Mapper");
        Assert.Contains(playlists, playlist => playlist.Name == "TEST_PLAYLIST" && playlist.SongCount == 2);
    }
}
