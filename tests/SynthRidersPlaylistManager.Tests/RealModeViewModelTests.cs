using SynthRidersPlaylistManager.App.Services;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Core.Services;

namespace SynthRidersPlaylistManager.Tests;

public sealed class RealModeViewModelTests
{
    [Fact]
    public async Task RealSnapshotReplacesBothPaneLibraryAndDisablesWritesButKeepsChecksAndSearch()
    {
        var hashA = new string('a', 64);
        var hashB = new string('b', 64);
        Song[] songs =
        [
            new(new(SongKind.Custom, hashA), "検索曲", "日本語Artist", "Mapper One", 120, TimeSpan.FromSeconds(180), "Unknown", true, ["real-list"], null, hashA, SongAvailability.Available),
            new(new(SongKind.Custom, hashB), "Another", "Artist", "検索Mapper", 130, TimeSpan.FromSeconds(200), "Unknown", false, [], null, hashB, SongAvailability.Available)
        ];
        var snapshot = new LibrarySnapshot(songs, [new("real.playlist", "real-list", 1)], [], [], new(2, 2, 0, 1, 0, 1, 0, 0, 0));
        var environment = new EnvironmentDiscoveryResult([], "test", DateTimeOffset.Now);
        var vm = new MainViewModel(new MockLibraryDataSource(), new EnvironmentStub(environment), null, new ReaderStub(snapshot));

        await vm.InitializeEnvironmentAsync();

        Assert.True(vm.IsRealDataMode);
        Assert.Equal(2, vm.Songs.Count);
        Assert.False(vm.CanManagePlaylists);
        Assert.False(vm.IsDragDropEnabled);
        Assert.False(vm.ToggleFavoriteCommand.CanExecute(vm.Songs[0]));
        vm.PaneA.SearchText = "日本語artist";
        Assert.Single(vm.PaneA.VisibleSongs.Cast<object>());
        vm.PaneA.SearchText = "検索Mapper";
        Assert.Single(vm.PaneA.VisibleSongs.Cast<object>());
        vm.PaneA.ToggleCheckedCommand.Execute(vm.Songs[1]);
        Assert.True(vm.Songs[1].IsSourceChecked);
        Assert.Same(vm.Songs, vm.PaneA.Owner.Songs);
        Assert.Same(vm.Songs, vm.PaneB.Owner.Songs);
    }

    [Fact]
    public async Task RealBlacklistUsesDetectedRootAndReturnedGameStateWithoutEnablingPlaylistWrites()
    {
        var now = DateTimeOffset.Now;
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fixture-game-root");
        var env = new EnvironmentDiscoveryResult([new(DataLocationKind.GameRoot, root, DataLocationSource.Derived, DataLocationStatus.Available, now, "fixture", false)], "fixture", now);
        Song[] songs = [new(new(SongKind.Custom, "a"), "A", "Artist", "Mapper", null, null, "", true, [], null, FileName: "a.synth", IsBlacklisted: true)];
        var snapshot = new LibrarySnapshot(songs, [], [], [], new(1, 1, 0, 1, 0, 0, 0, 0, 0));
        var store = new FlagsStub();
        using var vm = new MainViewModel(new MockLibraryDataSource(), new EnvironmentStub(env), null, new ReaderStub(snapshot), songFlagsStore: store);
        await vm.InitializeEnvironmentAsync();
        vm.PaneB.SelectedCollection = vm.SmartNavigation.Single(x => x.Filter == NavigationFilter.Blacklist);
        var song = Assert.Single(vm.PaneB.VisibleSongs.Cast<SongItemViewModel>());
        vm.PaneB.ToggleCheckedCommand.Execute(song);
        vm.PaneB.RemoveBlacklistCommand.Execute(null);
        Assert.Equal(root, store.Root);
        Assert.Equal("a.synth", Assert.Single(store.FileNames));
        Assert.False(song.IsBlacklisted);
        Assert.Empty(vm.PaneB.VisibleSongs.Cast<object>());
        Assert.Same(song, Assert.Single(vm.PaneA.VisibleSongs.Cast<SongItemViewModel>()));
        Assert.True(song.IsFavorite);
        Assert.False(vm.CanManagePlaylists);
        Assert.False(vm.IsDragDropEnabled);
        store.Fail = true;
        vm.PaneB.SelectedCollection = vm.SmartNavigation.First(x => x.Filter == NavigationFilter.AllSongs);
        vm.PaneB.AddBlacklistCommand.Execute(null);
        Assert.False(song.IsBlacklisted);
        store.IsGameStopped = false;
        Assert.False(vm.PaneB.AddBlacklistCommand.CanExecute(null));
    }

    private sealed class FlagsStub : ISongFlagsStore
    {
        public bool IsGameStopped { get; set; } = true;
        public bool Fail { get; set; }
        public string? Root { get; private set; }
        public IReadOnlyCollection<string> FileNames { get; private set; } = [];
        public IReadOnlySet<string> SetFavorites(string path, IReadOnlyCollection<string> entries, bool enabled) => throw new NotSupportedException();
        public IReadOnlySet<string> SetBlacklist(string gameRoot, IReadOnlyCollection<string> fileNames, bool enabled)
        {
            if (Fail) throw new System.IO.IOException();
            Root = gameRoot; FileNames = fileNames;
            return enabled ? fileNames.ToHashSet(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        }
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
}
