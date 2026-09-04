using System.Text.Json;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Infrastructure.EnvironmentDiscovery;

public sealed record DiscoverySettings(Dictionary<DataLocationKind, string> ManualOverrides)
{
    public static DiscoverySettings Empty { get; } = new([]);
}

public interface IDiscoverySettingsStore
{
    Task<DiscoverySettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(DiscoverySettings settings, CancellationToken cancellationToken = default);
}

public sealed class JsonDiscoverySettingsStore(string settingsPath) : IDiscoverySettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string SettingsPath { get; } = settingsPath;

    public async Task<DiscoverySettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(SettingsPath)) return DiscoverySettings.Empty;
            await using var stream = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<DiscoverySettings>(stream, Options, cancellationToken) ?? DiscoverySettings.Empty;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return DiscoverySettings.Empty;
        }
    }

    public async Task SaveAsync(DiscoverySettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await using var stream = new FileStream(SettingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken);
    }

    public static JsonDiscoverySettingsStore CreateDefault()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new(Path.Combine(root, "SynthRidersPlaylistManager", "settings.json"));
    }
}
