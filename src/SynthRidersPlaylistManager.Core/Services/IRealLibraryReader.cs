using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Core.Services;

public interface IRealLibraryReader
{
    Task<LibrarySnapshot> LoadAsync(EnvironmentDiscoveryResult environment, CancellationToken cancellationToken = default);
}
