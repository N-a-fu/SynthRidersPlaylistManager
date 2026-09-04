namespace SynthRidersPlaylistManager.Core.Services;

public interface ISongFlagsStore
{
    bool IsGameStopped { get; }
    IReadOnlySet<string> SetBlacklist(string gameRoot, IReadOnlyCollection<string> fileNames, bool enabled);
    IReadOnlySet<string> SetFavorites(string favoritesPath, IReadOnlyCollection<string> entries, bool enabled);
}
