using SynthRidersPlaylistManager.App.Services;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Core.Services;

namespace SynthRidersPlaylistManager.Tests;

public sealed class PlaylistPersistenceViewModelTests
{
    [Fact]
    public async Task RealModeRoutesCreateRenameDropRemoveAndDeleteToPlaylistStore()
    {
        var hashA = new string('a', 64); var hashB = new string('b', 64);
        Song[] songs =
        [
            new(new(SongKind.Custom, hashA), "A", "Artist", "Mapper", null, TimeSpan.FromSeconds(100), "", false, ["Other"], null, hashA),
            new(new(SongKind.Custom, hashB), "B", "Artist", "Mapper", null, TimeSpan.FromSeconds(110), "", true, ["Existing"], null, hashB)
        ];
        var snapshot = new LibrarySnapshot(songs, [new("000005__existing.playlist", "Existing", 1)], [], [], new(2, 2, 0, 0, 0, 1, 0, 0, 0));
        var now = DateTimeOffset.Now;
        using var playlistDirectory = new TempDirectory();
        var env = new EnvironmentDiscoveryResult([new(DataLocationKind.Playlists, playlistDirectory.Path, DataLocationSource.Derived, DataLocationStatus.Available, now, "fixture", false)], "fixture", now);
        var store = new StoreStub();
        using var vm = new MainViewModel(new MockLibraryDataSource(), new EnvironmentStub(env), null, new ReaderStub(snapshot), playlistStore: store);
        await vm.InitializeEnvironmentAsync();

        vm.PaneA.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs);
        vm.PaneB.SelectedCollection = vm.PlaylistNavigation.Single();
        vm.PaneB.DropCommand.Execute(vm.PaneA.CreateDragPayload(vm.Songs[0]));
        Assert.Equal(hashA, Assert.Single(store.Added).Hash);
        Assert.Contains("Existing", vm.Songs[0].PlaylistNames);

        vm.PaneB.ToggleCheckedCommand.Execute(vm.Songs[0]);
        vm.PaneB.RemoveFromCurrentPlaylistCommand.Execute(null);
        Assert.Equal(hashA, Assert.Single(store.Removed));
        Assert.DoesNotContain("Existing", vm.Songs[0].PlaylistNames);
        Assert.Contains("Other", vm.Songs[0].PlaylistNames);
        Assert.True(vm.Songs[1].IsFavorite);

        vm.PlaylistNameDraft = "New List"; vm.CreatePlaylistCommand.Execute(null);
        Assert.Contains(vm.PlaylistNavigation, x => x.PlaylistName == "New List");
        vm.PlaylistNameDraft = "Renamed List"; vm.RenamePlaylistCommand.Execute(null);
        Assert.Contains(vm.PlaylistNavigation, x => x.PlaylistName == "Renamed List");
        var renamed = vm.SelectedDestinationNavigation!;
        vm.RequestDeletePlaylistCommand.Execute(renamed); vm.ConfirmDeletePlaylistCommand.Execute(null);
        Assert.Equal("000006__renamedlist.playlist", store.Deleted);

        vm.GameState = GameAccessState.RunningReadOnly;
        Assert.False(vm.CreatePlaylistCommand.CanExecute(null));
        Assert.False(vm.PaneB.RemoveFromCurrentPlaylistCommand.CanExecute(null));
    }

    private sealed class StoreStub : IPlaylistStore
    {
        public bool IsGameStopped => true;
        public IReadOnlyCollection<PlaylistWriteSong> Added { get; private set; } = [];
        public IReadOnlyCollection<string> Removed { get; private set; } = [];
        public string? Deleted { get; private set; }
        public PlaylistFileReference Create(string directory, string name) => new("000006__newlist.playlist", name);
        public PlaylistFileReference Rename(string directory, string fileName, string newName) => new("000006__renamedlist.playlist", newName);
        public void Delete(string directory, string fileName) => Deleted = fileName;
        public void AddSongs(string directory, string fileName, IReadOnlyCollection<PlaylistWriteSong> songs) => Added = songs;
        public void RemoveSongs(string directory, string fileName, IReadOnlyCollection<string> hashes) => Removed = hashes;
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
    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "srpm-vm-" + Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(Path); }
        public string Path { get; }
        public void Dispose() => System.IO.Directory.Delete(Path, true);
    }
}
