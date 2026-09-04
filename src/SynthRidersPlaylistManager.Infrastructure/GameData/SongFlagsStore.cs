using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using SynthRidersPlaylistManager.Core.Services;

namespace SynthRidersPlaylistManager.Infrastructure.GameData;

// Only verified string-array fields are editable. All other JSON values are retained.
public sealed class SongFlagsStore(Func<bool>? isGameStopped = null) : ISongFlagsStore
{
    private readonly Func<bool> _isGameStopped = isGameStopped ?? CheckStopped;
    public bool IsGameStopped => _isGameStopped();
    private static bool CheckStopped()
    {
        try
        {
            var processes = Process.GetProcessesByName("SynthRiders");
            try { return processes.Length == 0; }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        catch { return false; }
    }

    public static IReadOnlySet<string> ReadBlacklist(string gameRoot)
    {
        var root = Parse(File.ReadAllBytes(Path.Combine(gameRoot, "twitchsettings.bin")));
        return ReadArray(root, "Blacklist").ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlySet<string> SetBlacklist(string gameRoot, IReadOnlyCollection<string> fileNames, bool enabled)
    {
        if (fileNames.Any(n => string.IsNullOrWhiteSpace(n) || n != Path.GetFileName(n) || !n.EndsWith(".synth", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Invalid SynthDB file_name.");
        return UpdateArray(Path.Combine(gameRoot, "twitchsettings.bin"), "Blacklist", fileNames, enabled, s => s, StringComparer.Ordinal);
    }

    public IReadOnlySet<string> SetFavorites(string favoritesPath, IReadOnlyCollection<string> entries, bool enabled)
    {
        if (entries.Any(e => FavoriteKey(e) is null)) throw new InvalidDataException("Invalid Favorite reference.");
        return UpdateArray(favoritesPath, "Favorite", entries, enabled, FavoriteKey, StringComparer.OrdinalIgnoreCase);
    }

    private static string? FavoriteKey(string entry) =>
        System.Text.RegularExpressions.Regex.IsMatch(entry, @"^.+-.+-[0-9a-fA-F]{64}$") ? entry[^64..] : null;

    private IReadOnlySet<string> UpdateArray(string path, string field, IReadOnlyCollection<string> entries, bool enabled,
        Func<string, string?> key, StringComparer comparer)
    {
        lock (WriteLock)
        {
            EnsureStopped();
            var original = File.ReadAllBytes(path);
            var root = Parse(original);
            var values = ReadArray(root, field).ToList();
            var targets = entries.Select(key).ToHashSet(comparer);
            if (enabled)
            {
                foreach (var entry in entries)
                {
                    var matches = values.Where(v => comparer.Equals(key(v), key(entry))).ToArray();
                    if (matches.Length == 0) values.Add(entry);
                    else if (matches.Length > 1)
                    {
                        var keep = matches[0];
                        values.RemoveAll(v => comparer.Equals(key(v), key(entry)));
                        values.Add(keep);
                    }
                }
            }
            else values.RemoveAll(v => key(v) is { } k && targets.Contains(k));
            var replacement = new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
            if (JsonNode.DeepEquals(root[field], replacement)) return values.ToHashSet(StringComparer.Ordinal);
            root[field] = replacement;
            var expected = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            var temporary = path + ".srpm-" + Guid.NewGuid().ToString("N") + ".tmp";
            var backup = path + ".srpm-" + Guid.NewGuid().ToString("N") + ".bak";
            try
            {
                EnsureStopped();
                File.WriteAllBytes(backup, original);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(expected);
                    stream.Write(bytes); stream.Flush(true);
                }
                var verified = Parse(File.ReadAllBytes(temporary));
                if (!JsonNode.DeepEquals(root, verified)) throw new InvalidDataException("JSON round-trip validation failed.");
                EnsureStopped();
                if (!original.AsSpan().SequenceEqual(File.ReadAllBytes(path))) throw new IOException("Game data changed externally; retry after rescan.");
                File.Replace(temporary, path, null);
                return ReadArray(verified, field).ToHashSet(StringComparer.Ordinal);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
    private static readonly object WriteLock = new();
    private void EnsureStopped() { if (!IsGameStopped) throw new InvalidOperationException("Game running or process state unavailable."); }
    private static JsonObject Parse(byte[] bytes) => JsonNode.Parse(bytes) as JsonObject ?? throw new InvalidDataException("Expected JSON object.");
    private static IEnumerable<string> ReadArray(JsonObject root, string field)
    {
        if (root[field] is not JsonArray array) throw new InvalidDataException("Expected an existing string array.");
        return array.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : throw new InvalidDataException("Expected string array entries.")).ToArray();
    }
}
