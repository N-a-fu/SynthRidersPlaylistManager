namespace SynthRidersPlaylistManager.Core.Models;

public readonly record struct SongIdentity(SongKind Kind, string StableId)
{
    public override string ToString() => $"{Kind}:{StableId}";
}
