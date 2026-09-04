using System.IO;
using System.Text;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Infrastructure.EnvironmentDiscovery;

namespace SynthRidersPlaylistManager.Tests;

public sealed class EnvironmentDiscoveryTests
{
    [Fact]
    public async Task ValidSteamLibraryAutoDetectsGameAndIndependentLocations()
    {
        using var fixture = new DiscoveryFixture();
        fixture.CreateValidInstallation();

        var result = await fixture.Service.DiscoverAsync();

        Assert.Equal(DataLocationStatus.Available, result.Find(DataLocationKind.GameRoot)?.Status);
        Assert.Equal(DataLocationStatus.Available, result.Find(DataLocationKind.Playlists)?.Status);
        Assert.Equal(DataLocationStatus.Available, result.Find(DataLocationKind.Favorites)?.Status);
        Assert.Equal(DataLocationStatus.Available, result.Find(DataLocationKind.CustomSongs)?.Status);
        Assert.Equal(DataLocationStatus.Available, result.Find(DataLocationKind.SynthDatabase)?.Status);
        Assert.Equal(DataLocationStatus.Available, result.Find(DataLocationKind.ImagesCache)?.Status);
        Assert.Equal(DataLocationStatus.Available, result.Find(DataLocationKind.TempAudio)?.Status);
    }

    [Fact]
    public async Task MissingGamePathDoesNotCrashAndRemainsNotConfigured()
    {
        using var fixture = new DiscoveryFixture();

        var result = await fixture.Service.DiscoverAsync();

        Assert.Equal(DataLocationStatus.NotConfigured, result.Find(DataLocationKind.GameRoot)?.Status);
        Assert.All(result.Locations.Where(x => x.Kind != DataLocationKind.GameRoot), x => Assert.Equal(DataLocationStatus.NotConfigured, x.Status));
    }

    [Fact]
    public void InvalidGameRootIsRejectedByIdentifyingStructure()
    {
        using var fixture = new DiscoveryFixture();
        var wrong = Directory.CreateDirectory(Path.Combine(fixture.Root, "wrong")).FullName;

        var result = fixture.Validator.Validate(DataLocationKind.GameRoot, wrong, DataLocationSource.UserOverride, true);

        Assert.Equal(DataLocationStatus.Invalid, result.Status);
    }

    [Fact]
    public void MissingDirectoryOnAvailableStorageIsReportedAsMissing()
    {
        using var fixture = new DiscoveryFixture();
        var missing = Path.Combine(fixture.Root, "missing-custom-songs");

        var result = fixture.Validator.Validate(DataLocationKind.CustomSongs, missing, DataLocationSource.UserOverride, true);

        Assert.Equal(DataLocationStatus.Missing, result.Status);
        Assert.Equal(Path.GetFullPath(missing), result.ResolvedPath);
    }

    [Fact]
    public async Task ValidManualOverrideTakesPrecedenceOverDerivedLocation()
    {
        using var fixture = new DiscoveryFixture();
        fixture.CreateValidInstallation();
        var customOverride = Directory.CreateDirectory(Path.Combine(fixture.Root, "external-custom")).FullName;
        File.WriteAllText(Path.Combine(customOverride, "manual.synth"), "fixture");

        var result = await fixture.Service.SetManualOverrideAsync(DataLocationKind.CustomSongs, customOverride);
        var location = result.Find(DataLocationKind.CustomSongs)!;

        Assert.Equal(Path.GetFullPath(customOverride), location.ResolvedPath);
        Assert.Equal(DataLocationSource.UserOverride, location.Source);
        Assert.True(location.IsUserOverride);
    }

    [Fact]
    public async Task InvalidManualPathIsNotPersisted()
    {
        using var fixture = new DiscoveryFixture();
        fixture.CreateValidInstallation();
        var invalid = Directory.CreateDirectory(Path.Combine(fixture.Root, "not-a-game")).FullName;

        var result = await fixture.Service.SetManualOverrideAsync(DataLocationKind.GameRoot, invalid);
        var saved = await fixture.Settings.LoadAsync();

        Assert.Equal(DataLocationStatus.Invalid, result.Find(DataLocationKind.GameRoot)?.Status);
        Assert.False(saved.ManualOverrides.ContainsKey(DataLocationKind.GameRoot));
    }

    [Fact]
    public async Task SavedUnavailableOverrideKeepsPathAndDoesNotBecomeMissing()
    {
        using var fixture = new DiscoveryFixture();
        var unavailablePath = Path.Combine(fixture.Root, "detached", "CustomSongs");
        await fixture.Settings.SaveAsync(new(new() { [DataLocationKind.CustomSongs] = unavailablePath }));
        fixture.FileSystem.UnavailablePaths.Add(Path.GetFullPath(unavailablePath));

        var result = await fixture.Service.DiscoverAsync();
        var location = result.Find(DataLocationKind.CustomSongs)!;

        Assert.Equal(DataLocationStatus.Unavailable, location.Status);
        Assert.Equal(Path.GetFullPath(unavailablePath), location.ResolvedPath);
        Assert.Equal(DataLocationSource.UserOverride, location.Source);
        Assert.DoesNotContain("Song Missing", location.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RevalidationReflectsStorageBecomingUnavailableWithoutDeletingSetting()
    {
        using var fixture = new DiscoveryFixture();
        fixture.CreateValidInstallation();
        var customOverride = Directory.CreateDirectory(Path.Combine(fixture.Root, "portable-custom")).FullName;
        File.WriteAllText(Path.Combine(customOverride, "portable.synth"), "fixture");
        await fixture.Service.SetManualOverrideAsync(DataLocationKind.CustomSongs, customOverride);

        fixture.FileSystem.UnavailablePaths.Add(Path.GetFullPath(customOverride));
        var result = await fixture.Service.DiscoverAsync();
        var saved = await fixture.Settings.LoadAsync();

        Assert.Equal(DataLocationStatus.Unavailable, result.Find(DataLocationKind.CustomSongs)?.Status);
        Assert.Equal(Path.GetFullPath(customOverride), saved.ManualOverrides[DataLocationKind.CustomSongs]);
    }

    [Fact]
    public async Task DiscoverySkipsBrokenSteamCandidateAndFindsNextCandidate()
    {
        using var fixture = new DiscoveryFixture();
        fixture.CreateValidInstallation();
        var broken = Directory.CreateDirectory(Path.Combine(fixture.Root, "broken-steam")).FullName;
        fixture.FileSystem.ThrowOnDirectoryPaths.Add(Path.GetFullPath(broken));
        fixture.SteamCandidates.Insert(0, broken);

        var result = await fixture.Service.DiscoverAsync();

        Assert.Equal(DataLocationStatus.Available, result.Find(DataLocationKind.GameRoot)?.Status);
    }

    [Fact]
    public void JsonAndSqliteValidationFailuresReturnStateInsteadOfThrowing()
    {
        using var fixture = new DiscoveryFixture();
        var badFavorite = Path.Combine(fixture.Root, "favorites.bin");
        var badDb = Path.Combine(fixture.Root, "SynthDB");
        File.WriteAllText(badFavorite, "not-json");
        File.WriteAllText(badDb, "not-sqlite");

        var favorite = fixture.Validator.Validate(DataLocationKind.Favorites, badFavorite, DataLocationSource.UserOverride, true);
        var database = fixture.Validator.Validate(DataLocationKind.SynthDatabase, badDb, DataLocationSource.UserOverride, true);

        Assert.Equal(DataLocationStatus.Invalid, favorite.Status);
        Assert.Equal(DataLocationStatus.Invalid, database.Status);
    }

    private sealed class DiscoveryFixture : IDisposable
    {
        public DiscoveryFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"srpm-phase2a-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            SteamRoot = Directory.CreateDirectory(Path.Combine(Root, "steam")).FullName;
            LibraryRoot = Directory.CreateDirectory(Path.Combine(Root, "library")).FullName;
            Settings = new(Path.Combine(Root, "app-settings.json"));
            SteamCandidates = [SteamRoot];
            FileSystem = new TestFileSystemProbe();
            Validator = new(FileSystem);
            Service = new(FileSystem, Settings, () => SteamCandidates);
        }

        public string Root { get; }
        public string SteamRoot { get; }
        public string LibraryRoot { get; }
        public List<string> SteamCandidates { get; }
        public TestFileSystemProbe FileSystem { get; }
        public JsonDiscoverySettingsStore Settings { get; }
        public DataLocationValidator Validator { get; }
        public SteamEnvironmentDiscoveryService Service { get; }

        public void CreateValidInstallation()
        {
            var steamApps = Directory.CreateDirectory(Path.Combine(SteamRoot, "steamapps")).FullName;
            File.WriteAllText(Path.Combine(steamApps, "libraryfolders.vdf"), $"\"libraryfolders\"\n{{\n \"1\" {{ \"path\" \"{LibraryRoot.Replace("\\", "\\\\", StringComparison.Ordinal)}\" }}\n}}");
            var libraryApps = Directory.CreateDirectory(Path.Combine(LibraryRoot, "steamapps")).FullName;
            File.WriteAllText(Path.Combine(libraryApps, $"appmanifest_{SteamEnvironmentDiscoveryService.SynthRidersAppId}.acf"), "\"AppState\" { \"appid\" \"885000\" \"installdir\" \"SynthRidersTest\" }");
            var game = Directory.CreateDirectory(Path.Combine(libraryApps, "common", "SynthRidersTest")).FullName;
            File.WriteAllBytes(Path.Combine(game, "SynthRiders.exe"), []);
            Directory.CreateDirectory(Path.Combine(game, "SynthRiders_Data"));
            var playlists = Directory.CreateDirectory(Path.Combine(game, "Playlist")).FullName;
            File.WriteAllText(Path.Combine(playlists, "fixture.playlist"), "{}");
            File.WriteAllText(Path.Combine(game, "favorites.bin"), "{\"Favorite\":[]}");
            var uc = Directory.CreateDirectory(Path.Combine(game, "SynthRidersUC")).FullName;
            var custom = Directory.CreateDirectory(Path.Combine(uc, "CustomSongs")).FullName;
            File.WriteAllText(Path.Combine(custom, "fixture.synth"), "fixture");
            File.WriteAllBytes(Path.Combine(uc, "SynthDB"), Encoding.ASCII.GetBytes("SQLite format 3\0fixture"));
            var images = Directory.CreateDirectory(Path.Combine(uc, "ImagesCache")).FullName;
            File.WriteAllBytes(Path.Combine(images, "fixture.png"), []);
            Directory.CreateDirectory(Path.Combine(uc, "tempExt"));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    private sealed class TestFileSystemProbe : IFileSystemProbe
    {
        private readonly PhysicalFileSystemProbe _physical = new();
        public HashSet<string> UnavailablePaths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ThrowOnDirectoryPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool DirectoryExists(string path)
        {
            if (ThrowOnDirectoryPaths.Contains(Path.GetFullPath(path))) throw new IOException("simulated removable-drive failure");
            return _physical.DirectoryExists(path);
        }
        public bool FileExists(string path) => _physical.FileExists(path);
        public bool IsStorageAvailable(string path) => !UnavailablePaths.Contains(Path.GetFullPath(path)) && _physical.IsStorageAvailable(path);
        public IEnumerable<string> EnumerateFiles(string path, string pattern) => _physical.EnumerateFiles(path, pattern);
        public string ReadAllText(string path) => _physical.ReadAllText(path);
        public byte[] ReadPrefix(string path, int length) => _physical.ReadPrefix(path, length);
    }
}
