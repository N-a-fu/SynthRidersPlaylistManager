using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Core.Services;

namespace SynthRidersPlaylistManager.Infrastructure.GameData;

public sealed partial class PlaylistStore(Func<bool>? isGameStopped = null) : IPlaylistStore
{
    private static readonly object WriteLock = new();
    private readonly Func<bool> _isGameStopped = isGameStopped ?? CheckStopped;
    public bool IsGameStopped => _isGameStopped();

    public PlaylistFileReference Create(string directory, string name)
    {
        ValidateDirectory(directory); name = ValidateName(name);
        lock (WriteLock)
        {
            EnsureStopped();
            if (FindByName(directory, name) is not null) throw new IOException("A playlist with the same name already exists.");
            var next = Directory.EnumerateFiles(directory, "*.playlist", SearchOption.TopDirectoryOnly)
                .Select(path => Prefix().Match(Path.GetFileName(path))).Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max() + 1;
            var fileName = $"{next:000000}__{FileStem(name)}.playlist";
            var path = SafePath(directory, fileName);
            var root = new JsonObject
            {
                ["dataString"] = new JsonArray(), ["SelectedIconIndex"] = 0, ["SelectedTexture"] = 0,
                ["namePlaylist"] = name, ["description"] = "New Playlist",
                ["gradientTop"] = "#324947", ["gradientDown"] = "#0C1203",
                ["colorTitle"] = "#FFFFFF", ["colorTexture"] = "#10180F",
                ["creationDate"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
            };
            WriteNew(path, root);
            return new(fileName, name);
        }
    }

    public PlaylistFileReference Rename(string directory, string fileName, string newName)
    {
        ValidateDirectory(directory); newName = ValidateName(newName);
        lock (WriteLock)
        {
            EnsureStopped();
            var source = SafePath(directory, fileName);
            var root = Parse(File.ReadAllBytes(source));
            var current = RequiredName(root);
            if (!string.Equals(current, newName, StringComparison.OrdinalIgnoreCase) && FindByName(directory, newName) is not null)
                throw new IOException("A playlist with the same name already exists.");
            root["namePlaylist"] = newName;
            var match = Prefix().Match(fileName);
            if (!match.Success) throw new InvalidDataException("Playlist file prefix is invalid.");
            var targetName = $"{match.Groups[1].Value}__{FileStem(newName)}.playlist";
            var target = SafePath(directory, targetName);
            if (!string.Equals(source, target, StringComparison.OrdinalIgnoreCase) && File.Exists(target)) throw new IOException("Target playlist file already exists.");
            ReplaceOrRename(source, target, root);
            return new(targetName, newName);
        }
    }

    public void Delete(string directory, string fileName)
    {
        ValidateDirectory(directory);
        lock (WriteLock)
        {
            EnsureStopped();
            var path = SafePath(directory, fileName);
            _ = RequiredName(Parse(File.ReadAllBytes(path)));
            var backup = BackupPath(path);
            File.Copy(path, backup, false);
            EnsureStopped();
            File.Delete(path);
        }
    }

    public void AddSongs(string directory, string fileName, IReadOnlyCollection<PlaylistWriteSong> songs) => Update(directory, fileName, root =>
    {
        var data = RequiredData(root);
        var existing = data.Select(Hash).Where(h => h is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var song in songs)
        {
            ValidateHash(song.Hash);
            if (!existing.Add(song.Hash)) continue;
            data.Add(new JsonObject
            {
                ["hash"] = song.Hash.ToLowerInvariant(), ["name"] = song.Title, ["author"] = song.Artist,
                ["beatmapper"] = song.Mapper, ["difficulty"] = 0, ["trackDuration"] = song.DurationSeconds,
                ["addedTime"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }
    });

    public void RemoveSongs(string directory, string fileName, IReadOnlyCollection<string> hashes) => Update(directory, fileName, root =>
    {
        foreach (var hash in hashes) ValidateHash(hash);
        var targets = hashes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var data = RequiredData(root);
        for (var i = data.Count - 1; i >= 0; i--) if (Hash(data[i]) is { } hash && targets.Contains(hash)) data.RemoveAt(i);
    });

    private void Update(string directory, string fileName, Action<JsonObject> mutate)
    {
        ValidateDirectory(directory);
        lock (WriteLock)
        {
            EnsureStopped();
            var path = SafePath(directory, fileName);
            var root = Parse(File.ReadAllBytes(path));
            _ = RequiredName(root); _ = RequiredData(root);
            mutate(root);
            Replace(path, root);
        }
    }

    private void Replace(string path, JsonObject root)
    {
        var original = File.ReadAllBytes(path);
        var temp = TemporaryPath(path); var backup = BackupPath(path);
        try
        {
            File.WriteAllBytes(backup, original);
            WriteAndValidate(temp, root);
            EnsureStopped();
            if (!original.AsSpan().SequenceEqual(File.ReadAllBytes(path))) throw new IOException("Playlist changed externally; rescan and retry.");
            File.Replace(temp, path, null);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void ReplaceOrRename(string source, string target, JsonObject root)
    {
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase)) { Replace(source, root); return; }
        var original = File.ReadAllBytes(source); var temp = TemporaryPath(source); var backup = BackupPath(source);
        try
        {
            File.WriteAllBytes(backup, original); WriteAndValidate(temp, root); EnsureStopped();
            if (!original.AsSpan().SequenceEqual(File.ReadAllBytes(source))) throw new IOException("Playlist changed externally; rescan and retry.");
            File.Move(source, backup + ".original");
            try { File.Move(temp, target); }
            catch { File.Move(backup + ".original", source); throw; }
            File.Delete(backup + ".original");
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void WriteNew(string path, JsonObject root)
    {
        var temp = TemporaryPath(path);
        try { WriteAndValidate(temp, root); EnsureStopped(); File.Move(temp, path); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static void WriteAndValidate(string path, JsonObject root)
    {
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\n", "\r\n", StringComparison.Ordinal);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { var bytes = new UTF8Encoding(false).GetBytes(json); stream.Write(bytes); stream.Flush(true); }
        var verified = Parse(File.ReadAllBytes(path));
        if (!JsonNode.DeepEquals(root, verified) || RequiredData(verified) is null || string.IsNullOrWhiteSpace(RequiredName(verified))) throw new InvalidDataException("Playlist JSON validation failed.");
    }
    private static JsonObject Parse(byte[] bytes) => JsonNode.Parse(bytes) as JsonObject ?? throw new InvalidDataException("Expected playlist JSON object.");
    private static JsonArray RequiredData(JsonObject root) => root["dataString"] as JsonArray ?? throw new InvalidDataException("Playlist dataString is missing.");
    private static string RequiredName(JsonObject root) => root["namePlaylist"]?.GetValue<string>() is { Length: > 0 } name ? name : throw new InvalidDataException("Playlist name is missing.");
    private static string? Hash(JsonNode? node) => (node as JsonObject)?["hash"]?.GetValue<string>();
    private static void ValidateHash(string hash) { if (!HashPattern().IsMatch(hash)) throw new InvalidDataException("Playlist song hash is invalid."); }
    private static void ValidateDirectory(string directory) { if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Validated playlist directory is unavailable."); }
    private static string ValidateName(string name)
    {
        name = name.Trim();
        if (name.Length is 0 or > 80 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Any(char.IsControl)) throw new InvalidDataException("Playlist name is invalid.");
        return name;
    }
    private static string FileStem(string name) { var stem = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant(); return stem.Length == 0 ? "playlist" : stem; }
    private static string SafePath(string directory, string fileName)
    {
        if (fileName != Path.GetFileName(fileName) || !fileName.EndsWith(".playlist", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Playlist file name is invalid.");
        return Path.Combine(Path.GetFullPath(directory), fileName);
    }
    private static string? FindByName(string directory, string name) => Directory.EnumerateFiles(directory, "*.playlist").FirstOrDefault(path => { try { return string.Equals(RequiredName(Parse(File.ReadAllBytes(path))), name, StringComparison.OrdinalIgnoreCase); } catch { return false; } });
    private static string TemporaryPath(string path) => path + ".srpm-" + Guid.NewGuid().ToString("N") + ".tmp";
    private static string BackupPath(string path) => path + ".srpm-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N") + ".bak";
    private void EnsureStopped() { if (!IsGameStopped) throw new InvalidOperationException("Synth Riders is running or process state is unavailable."); }
    private static bool CheckStopped() { try { var processes = Process.GetProcessesByName("SynthRiders"); try { return processes.Length == 0; } finally { foreach (var process in processes) process.Dispose(); } } catch { return false; } }
    [GeneratedRegex(@"^(\d{6})__.+\.playlist$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Prefix();
    [GeneratedRegex(@"^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)] private static partial Regex HashPattern();
}
