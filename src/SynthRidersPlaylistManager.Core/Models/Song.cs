namespace SynthRidersPlaylistManager.Core.Models;

public sealed record Song(
    SongIdentity Identity,
    string Title,
    string Artist,
    string Mapper,
    double? Bpm,
    TimeSpan? Duration,
    string Difficulty,
    bool IsFavorite,
    IReadOnlyList<string> PlaylistNames,
    DateTimeOffset? AddedAt,
    string? GameHash = null,
    SongAvailability Availability = SongAvailability.Unknown,
    bool IsMetadataComplete = true,
    bool HasCover = false,
    bool HasAudio = false,
    CoverArtState CoverState = CoverArtState.Missing,
    string? CoverImagePath = null,
    string? CoverKey = null,
    AudioPreviewState AudioState = AudioPreviewState.Missing,
    string? AudioPreviewPath = null);
