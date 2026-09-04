namespace SynthRidersPlaylistManager.Core.Models;

public sealed record EnvironmentDiscoveryResult(
    IReadOnlyList<DataLocation> Locations,
    string Summary,
    DateTimeOffset CompletedAt)
{
    public DataLocation? Find(DataLocationKind kind) => Locations.FirstOrDefault(x => x.Kind == kind);
}
