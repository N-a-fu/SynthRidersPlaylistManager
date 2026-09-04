namespace SynthRidersPlaylistManager.Core.Models;

public sealed record PlaylistWriteSong(string Hash, string Title, string Artist, string Mapper, double DurationSeconds);

public sealed record PlaylistFileReference(string FileName, string Name);
