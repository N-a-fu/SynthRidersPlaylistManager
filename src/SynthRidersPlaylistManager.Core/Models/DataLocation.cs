namespace SynthRidersPlaylistManager.Core.Models;

public sealed record DataLocation(
    DataLocationKind Kind,
    string? ResolvedPath,
    DataLocationSource Source,
    DataLocationStatus Status,
    DateTimeOffset LastValidated,
    string ValidationMessage,
    bool IsUserOverride);
