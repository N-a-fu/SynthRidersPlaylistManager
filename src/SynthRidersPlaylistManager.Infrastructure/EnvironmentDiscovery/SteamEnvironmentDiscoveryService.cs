using System.Text.RegularExpressions;
using System.Runtime.Versioning;
using Microsoft.Win32;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Core.Services;

namespace SynthRidersPlaylistManager.Infrastructure.EnvironmentDiscovery;

public sealed partial class SteamEnvironmentDiscoveryService : IEnvironmentDiscoveryService
{
    public const string SynthRidersAppId = "885000";
    private readonly IFileSystemProbe _fileSystem;
    private readonly IDiscoverySettingsStore _settingsStore;
    private readonly DataLocationValidator _validator;
    private readonly Func<IEnumerable<string>> _steamRootCandidates;

    public SteamEnvironmentDiscoveryService(
        IFileSystemProbe fileSystem,
        IDiscoverySettingsStore settingsStore,
        Func<IEnumerable<string>> steamRootCandidates)
    {
        _fileSystem = fileSystem;
        _settingsStore = settingsStore;
        _validator = new(fileSystem);
        _steamRootCandidates = steamRootCandidates;
    }

    [SupportedOSPlatform("windows")]
    public static SteamEnvironmentDiscoveryService CreateDefault() => new(
        new PhysicalFileSystemProbe(), JsonDiscoverySettingsStore.CreateDefault(), DiscoverSteamRootsFromRegistry);

    public async Task<EnvironmentDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsStore.LoadAsync(cancellationToken);
        return await Task.Run(() => Discover(settings, cancellationToken), cancellationToken);
    }

    public async Task<EnvironmentDiscoveryResult> SetManualOverrideAsync(DataLocationKind kind, string path, CancellationToken cancellationToken = default)
    {
        var candidate = _validator.Validate(kind, path, DataLocationSource.UserOverride, true);
        var settings = await _settingsStore.LoadAsync(cancellationToken);
        if (candidate.Status is DataLocationStatus.Available or DataLocationStatus.Unknown)
        {
            var overrides = new Dictionary<DataLocationKind, string>(settings.ManualOverrides) { [kind] = candidate.ResolvedPath! };
            settings = new(overrides);
            await _settingsStore.SaveAsync(settings, cancellationToken);
            return await Task.Run(() => Discover(settings, cancellationToken), cancellationToken);
        }

        var current = await Task.Run(() => Discover(settings, cancellationToken), cancellationToken);
        var locations = current.Locations.Where(x => x.Kind != kind).Append(candidate).OrderBy(x => x.Kind).ToArray();
        return new(locations, "手動指定Pathを検証できなかったため保存していません。", DateTimeOffset.Now);
    }

    private EnvironmentDiscoveryResult Discover(DiscoverySettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var autoGameRoot = DetectGameRoot(cancellationToken);
        var gameRoot = ResolveLocation(DataLocationKind.GameRoot, settings, autoGameRoot?.ResolvedPath, DataLocationSource.AutoDetected);
        var locations = new List<DataLocation> { gameRoot };

        AddDerived(DataLocationKind.Playlists, "Playlist");
        AddDerived(DataLocationKind.Favorites, "favorites.bin");
        AddDerived(DataLocationKind.CustomSongs, Path.Combine("SynthRidersUC", "CustomSongs"));
        AddDerived(DataLocationKind.SynthDatabase, Path.Combine("SynthRidersUC", "SynthDB"));
        AddDerived(DataLocationKind.ImagesCache, Path.Combine("SynthRidersUC", "ImagesCache"));
        AddDerived(DataLocationKind.TempAudio, Path.Combine("SynthRidersUC", "tempExt"));

        var available = locations.Count(x => x.Status == DataLocationStatus.Available);
        var summary = gameRoot.Status == DataLocationStatus.Available
            ? $"Steam版Synth Ridersを検出しました。Data Location {available}/{locations.Count}件が利用可能です。Phase 2A Read-only。"
            : "Synth Ridersを自動検出できませんでした。再検出または検証済みの手動指定を利用してください。Mock UIは引き続き使用できます。";
        return new(locations.OrderBy(x => x.Kind).ToArray(), summary, DateTimeOffset.Now);

        void AddDerived(DataLocationKind kind, string relativePath)
        {
            var derived = gameRoot.Status == DataLocationStatus.Available && gameRoot.ResolvedPath is not null
                ? Path.Combine(gameRoot.ResolvedPath, relativePath)
                : null;
            locations.Add(ResolveLocation(kind, settings, derived, DataLocationSource.Derived));
        }
    }

    private DataLocation ResolveLocation(DataLocationKind kind, DiscoverySettings settings, string? automaticPath, DataLocationSource automaticSource)
    {
        if (settings.ManualOverrides.TryGetValue(kind, out var manualPath))
            return _validator.Validate(kind, manualPath, DataLocationSource.UserOverride, true);
        return automaticPath is null
            ? new(kind, null, DataLocationSource.NotResolved, DataLocationStatus.NotConfigured, DateTimeOffset.Now, "Locationを解決できませんでした。データ変更は行っていません。", false)
            : _validator.Validate(kind, automaticPath, automaticSource, false);
    }

    private DataLocation? DetectGameRoot(CancellationToken cancellationToken)
    {
        foreach (var steamRoot in _steamRootCandidates().Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!_fileSystem.DirectoryExists(steamRoot)) continue;
                foreach (var library in EnumerateLibraries(steamRoot))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var manifest = Path.Combine(library, "steamapps", $"appmanifest_{SynthRidersAppId}.acf");
                    if (!_fileSystem.FileExists(manifest)) continue;
                    var installDir = ReadQuotedValue(_fileSystem.ReadAllText(manifest), "installdir");
                    if (string.IsNullOrWhiteSpace(installDir)) continue;
                    var candidate = Path.Combine(library, "steamapps", "common", installDir);
                    var validated = _validator.Validate(DataLocationKind.GameRoot, candidate, DataLocationSource.AutoDetected, false);
                    if (validated.Status == DataLocationStatus.Available) return validated;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                // A single stale/inaccessible Steam candidate must not abort other libraries.
            }
        }
        return null;
    }

    private IEnumerable<string> EnumerateLibraries(string steamRoot)
    {
        yield return Path.GetFullPath(steamRoot);
        var metadata = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!_fileSystem.FileExists(metadata)) yield break;
        var text = _fileSystem.ReadAllText(metadata);
        foreach (Match match in VdfPathRegex().Matches(text))
        {
            var path = match.Groups["value"].Value.Replace("\\\\", "\\", StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(path)) yield return Path.GetFullPath(path);
        }
    }

    internal static string? ReadQuotedValue(string text, string key)
    {
        var pattern = $"\"{Regex.Escape(key)}\"\\s*\"(?<value>[^\"]+)\"";
        return Regex.Match(text, pattern, RegexOptions.IgnoreCase).Groups["value"].Value is { Length: > 0 } value ? value : null;
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> DiscoverSteamRootsFromRegistry()
    {
        var candidates = new List<string>();
        TryRead(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        TryRead(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        TryRead(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
        return candidates;

        void TryRead(RegistryKey hive, string subKey, string valueName)
        {
            try { using var key = hive.OpenSubKey(subKey); if (key?.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value)) candidates.Add(value); }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.SecurityException) { }
        }
    }

    [GeneratedRegex("\\\"path\\\"\\s*\\\"(?<value>[^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex VdfPathRegex();
}
