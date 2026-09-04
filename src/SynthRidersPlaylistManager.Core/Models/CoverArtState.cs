namespace SynthRidersPlaylistManager.Core.Models;

public enum CoverArtState
{
    Available,
    Missing,
    Unknown,
    ParentLocationUnavailable,
    InvalidImage,
    ConflictingMapping
}

public sealed record CoverArtDiagnostics(
    int ImageFiles,
    int ValidImages,
    int InvalidImages,
    int HashNamedImages,
    int DirectHashMatches,
    int ResolvedSongs,
    int MissingSongs,
    int OfficialSideResolved,
    int OfficialSideUnresolved,
    int OrphanImages,
    int ConflictingMappings,
    string FormatSummary,
    string DimensionSummary,
    bool LocationAvailable);
