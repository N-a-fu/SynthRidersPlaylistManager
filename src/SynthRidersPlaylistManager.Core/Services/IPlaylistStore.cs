using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Core.Services;

public interface IPlaylistStore
{
    bool IsGameStopped { get; }
    PlaylistFileReference Create(string directory, string name);
    PlaylistFileReference Rename(string directory, string fileName, string newName);
    void Delete(string directory, string fileName);
    void AddSongs(string directory, string fileName, IReadOnlyCollection<PlaylistWriteSong> songs);
    void RemoveSongs(string directory, string fileName, IReadOnlyCollection<string> hashes);
}
