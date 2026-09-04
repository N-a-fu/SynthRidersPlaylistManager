namespace SynthRidersPlaylistManager.Core.Models;

public sealed record LibraryDiagnostics(
    int TotalSongs,
    int CustomSongs,
    int OfficialSideOrUnknownSongs,
    int FavoritesResolved,
    int FavoritesUnresolved,
    int PlaylistEntriesResolved,
    int PlaylistEntriesUnresolved,
    int MissingSongs,
    int UnknownSongs,
    CoverArtDiagnostics? Covers = null);

public sealed record UnresolvedLibraryEntry(string Source, string? Hash, string? DisplayName, string Reason);

public sealed record LibrarySnapshot(
    IReadOnlyList<Song> Songs,
    IReadOnlyList<PlaylistSummary> Playlists,
    IReadOnlyList<UnresolvedLibraryEntry> UnresolvedEntries,
    IReadOnlyList<string> Warnings,
    LibraryDiagnostics Diagnostics);
