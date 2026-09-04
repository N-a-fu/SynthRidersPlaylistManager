using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Core.Services;

namespace SynthRidersPlaylistManager.App.Services;

public sealed class MockLibraryDataSource : ILibraryDataSource
{
    private static readonly string[] Titles =
    [
        "Neon Skyline", "Midnight Circuit", "Violet Pulse", "Afterglow", "Electric Bloom",
        "Gravity Shift", "Crystal Motion", "Parallel Hearts", "City Lights", "Orbiting You",
        "Static Dreams", "Golden Hourglass", "Digital Rain", "Runaway Signal", "Echo Chamber",
        "Lunar Arcade", "Northern Lights", "Chromatic Rush", "Zero Gravity", "Radiant Lines",
        "Night Drive", "Prism Break", "Silver Current", "Magnetic", "Starlit Avenue",
        "Frequency", "Velvet Thunder", "Moving Pictures"
    ];

    public IReadOnlyList<Song> GetSongs()
    {
        var songs = new List<Song>
        {
            Create("official-watch", SongKind.OfficialOrDlc, "TEST OFFICIAL SONG", "Test Artist", "", 119, 179, "Hard", true, ["TEST_PLAYLIST"]),
            Create("custom-blinding", SongKind.Custom, "TEST CUSTOM SONG", "Test Artist", "Test Mapper", 171, 202, "Master", true, ["TEST_PLAYLIST", "Workout"])
        };

        for (var index = 0; index < Titles.Length; index++)
        {
            var kind = index % 4 == 0 ? SongKind.OfficialOrDlc : SongKind.Custom;
            var playlists = index switch
            {
                2 or 8 => new[] { "Existing Playlist", "Workout", "Late Night" },
                5 or 11 or 17 => new[] { "Existing Playlist" },
                7 or 14 => new[] { "Workout" },
                _ => Array.Empty<string>()
            };
            songs.Add(Create(
                $"mock-{index:00}", kind, Titles[index], $"Artist {(char)('A' + index % 12)}",
                kind == SongKind.Custom ? $"Mapper {index % 7 + 1}" : string.Empty,
                90 + index * 3, 132 + index * 4, Difficulty(index), index % 3 != 1, playlists));
        }

        return songs;
    }

    public IReadOnlyList<PlaylistSummary> GetPlaylists() =>
    [
        new("mock-existing", "Existing Playlist", 5),
        new("mock-spm-test", "TEST_PLAYLIST", 2),
        new("mock-workout", "Workout", 5),
        new("mock-late-night", "Late Night", 1)
    ];

    private static Song Create(
        string id,
        SongKind kind,
        string title,
        string artist,
        string mapper,
        double bpm,
        int durationSeconds,
        string difficulty,
        bool favorite,
        IReadOnlyList<string> playlists) =>
        new(new SongIdentity(kind, id), title, artist, mapper, bpm, TimeSpan.FromSeconds(durationSeconds), difficulty,
            favorite, playlists, DateTimeOffset.Now.AddDays(-Math.Abs(id.GetHashCode() % 120)));

    private static string Difficulty(int index) => (index % 5) switch
    {
        0 => "Easy",
        1 => "Normal",
        2 => "Hard",
        3 => "Expert",
        _ => "Master"
    };
}
