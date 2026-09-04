using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Core.Services;

public interface ILibraryDataSource
{
    IReadOnlyList<Song> GetSongs();

    IReadOnlyList<PlaylistSummary> GetPlaylists();
}
