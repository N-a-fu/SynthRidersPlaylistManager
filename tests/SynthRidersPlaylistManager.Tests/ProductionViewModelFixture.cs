using System.IO;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Core.Services;

namespace SynthRidersPlaylistManager.Tests;

internal static class ProductionViewModelFixture
{
    private static readonly string Root = CreateRoot();

    public static MainViewModel Create()
    {
        var snapshot = CreateSnapshot();
        var now = DateTimeOffset.UtcNow;
        var environment = new EnvironmentDiscoveryResult(
        [
            new(DataLocationKind.GameRoot, Root, DataLocationSource.Derived, DataLocationStatus.Available, now, "fixture", false),
            new(DataLocationKind.Playlists, Root, DataLocationSource.Derived, DataLocationStatus.Available, now, "fixture", false),
            new(DataLocationKind.Favorites, Path.Combine(Root, "favorites.bin"), DataLocationSource.Derived, DataLocationStatus.Available, now, "fixture", false)
        ], "fixture", now);

        var viewModel = new MainViewModel(null, new EnvironmentStub(environment), null, new ReaderStub(snapshot),
            songFlagsStore: new SongFlagsStub(snapshot.Songs), playlistStore: new PlaylistStoreStub());
        viewModel.InitializeEnvironmentAsync().GetAwaiter().GetResult();
        return viewModel;
    }

    public static LibrarySnapshot CreateSnapshot()
    {
        var songs = new List<Song>();
        for (var index = 0; index < 8; index++)
        {
            var hash = index.ToString("x2").PadLeft(64, '0');
            var title = index switch { 1 => "Test Song Alpha", 2 => "Test Song Beta", _ => $"Fixture Song {index + 1}" };
            var mapper = index == 2 ? "Search Mapper" : $"Mapper {index + 1}";
            IReadOnlyList<string> memberships = index switch
            {
                1 => ["Test Playlist A"],
                2 => ["Test Playlist A", "Test Playlist B"],
                3 => ["Test Playlist B"],
                _ => []
            };
            songs.Add(new(new(SongKind.Custom, hash), title, $"Artist {index + 1}", mapper, 100 + index,
                TimeSpan.FromSeconds(150 + index), "Custom", index is 0 or 2, memberships,
                DateTimeOffset.UtcNow.AddDays(-index), hash, SongAvailability.Available,
                FileName: $"fixture-{index + 1}.synth", FavoriteReference: $"fixture-{index + 1}-{hash}"));
        }

        PlaylistSummary[] playlists =
        [
            new("000001__testa.playlist", "Test Playlist A", 2),
            new("000002__testb.playlist", "Test Playlist B", 2),
            new("000003__empty.playlist", "Empty Playlist", 0),
            new("000004__other.playlist", "Other Playlist", 0)
        ];
        return new(songs, playlists, [], [], new(songs.Count, songs.Count, 0, 0, 0, playlists.Length, 0, 0, 0));
    }

    private static string CreateRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "srpm-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class EnvironmentStub(EnvironmentDiscoveryResult result) : IEnvironmentDiscoveryService
    {
        public Task<EnvironmentDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<EnvironmentDiscoveryResult> SetManualOverrideAsync(DataLocationKind kind, string path, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class ReaderStub(LibrarySnapshot snapshot) : IRealLibraryReader
    {
        public Task<LibrarySnapshot> LoadAsync(EnvironmentDiscoveryResult environment, CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
    }

    private sealed class PlaylistStoreStub : IPlaylistStore
    {
        private int _next = 10;
        public bool IsGameStopped => true;
        public PlaylistFileReference Create(string directory, string name) => new($"{_next++:000000}__fixture.playlist", name);
        public PlaylistFileReference Rename(string directory, string fileName, string newName) => new(fileName, newName);
        public void Delete(string directory, string fileName) { }
        public void AddSongs(string directory, string fileName, IReadOnlyCollection<PlaylistWriteSong> songs) { }
        public void RemoveSongs(string directory, string fileName, IReadOnlyCollection<string> hashes) { }
    }

    private sealed class SongFlagsStub : ISongFlagsStore
    {
        private readonly HashSet<string> _blacklist = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
        public SongFlagsStub(IEnumerable<Song> songs)
        {
            _favorites.UnionWith(songs.Where(song => song.IsFavorite && !string.IsNullOrWhiteSpace(song.FavoriteReference))
                .Select(song => song.FavoriteReference!));
            _blacklist.UnionWith(songs.Where(song => song.IsBlacklisted && !string.IsNullOrWhiteSpace(song.FileName))
                .Select(song => song.FileName!));
        }
        public bool IsGameStopped => true;
        public IReadOnlySet<string> SetBlacklist(string gameRoot, IReadOnlyCollection<string> fileNames, bool enabled)
        {
            foreach (var value in fileNames) { if (enabled) _blacklist.Add(value); else _blacklist.Remove(value); }
            return _blacklist;
        }
        public IReadOnlySet<string> SetFavorites(string favoritesPath, IReadOnlyCollection<string> entries, bool enabled)
        {
            foreach (var value in entries) { if (enabled) _favorites.Add(value); else _favorites.Remove(value); }
            return _favorites;
        }
    }
}
