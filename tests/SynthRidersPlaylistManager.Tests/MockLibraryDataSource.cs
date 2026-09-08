using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Core.Services;

namespace SynthRidersPlaylistManager.App.Services;

// Test-only data. Production starts empty until validated game data is loaded.
public sealed class MockLibraryDataSource : ILibraryDataSource
{
    private static readonly string HashA = new('a', 64);
    private static readonly string HashB = new('b', 64);

    public IReadOnlyList<Song> GetSongs() =>
    [
        new(new(SongKind.Custom, HashA), "Test Song A", "Artist A", "Mapper A", 120, TimeSpan.FromSeconds(180), "Custom", true, ["Test Playlist A"], DateTimeOffset.UtcNow, HashA, SongAvailability.Available, FileName: "test-a.synth", FavoriteReference: $"Test Song A-Artist A-{HashA}"),
        new(new(SongKind.Custom, HashB), "Test Song B", "Artist B", "Mapper B", 130, TimeSpan.FromSeconds(200), "Custom", false, ["Test Playlist B"], DateTimeOffset.UtcNow, HashB, SongAvailability.Available, FileName: "test-b.synth", FavoriteReference: $"Test Song B-Artist B-{HashB}")
    ];

    public IReadOnlyList<PlaylistSummary> GetPlaylists() =>
    [
        new("test-a.playlist", "Test Playlist A", 1),
        new("test-b.playlist", "Test Playlist B", 1)
    ];
}
