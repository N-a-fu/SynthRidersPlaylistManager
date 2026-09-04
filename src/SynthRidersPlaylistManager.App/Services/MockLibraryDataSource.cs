using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Core.Services;

namespace SynthRidersPlaylistManager.App.Services;

public sealed class MockLibraryDataSource : ILibraryDataSource
{
    public IReadOnlyList<Song> GetSongs() => [];
    public IReadOnlyList<PlaylistSummary> GetPlaylists() => [];
}
