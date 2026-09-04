using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Core.Services;

public interface IEnvironmentDiscoveryService
{
    Task<EnvironmentDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default);
    Task<EnvironmentDiscoveryResult> SetManualOverrideAsync(DataLocationKind kind, string path, CancellationToken cancellationToken = default);
}
