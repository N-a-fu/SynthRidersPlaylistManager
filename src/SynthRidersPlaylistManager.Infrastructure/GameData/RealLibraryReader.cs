using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Core.Services;

namespace SynthRidersPlaylistManager.Infrastructure.GameData;

public sealed partial class RealLibraryReader : IRealLibraryReader
{
    public async Task<LibrarySnapshot> LoadAsync(EnvironmentDiscoveryResult environment, CancellationToken cancellationToken = default) =>
        await Task.Run(() => Load(environment, cancellationToken), cancellationToken);

    private static LibrarySnapshot Load(EnvironmentDiscoveryResult environment, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var unresolved = new List<UnresolvedLibraryEntry>();
        var database = RequireAvailable(environment, DataLocationKind.SynthDatabase);
        if (database is null)
            throw new InvalidOperationException("SynthDBが利用可能ではないため、実Libraryを安全に読み込めません。");

        var customLocation = environment.Find(DataLocationKind.CustomSongs);
        var imageLocation = environment.Find(DataLocationKind.ImagesCache);
        var audioLocation = environment.Find(DataLocationKind.TempAudio);
        var customFiles = ReadFileNames(customLocation, "*.synth", warnings);
        var covers = ReadCoverCatalog(imageLocation, warnings);

        var rows = ReadDatabase(database, cancellationToken);
        IReadOnlySet<string> blacklist = new HashSet<string>(StringComparer.Ordinal);
        var gameRoot = RequireAvailable(environment, DataLocationKind.GameRoot);
        if (gameRoot is not null)
        {
            try { blacklist = SongFlagsStore.ReadBlacklist(gameRoot); }
            catch (Exception e) when (IsReadFailure(e) || e is JsonException or InvalidDataException or InvalidOperationException)
            { warnings.Add("Blacklistを確認できません。再スキャン前に保存先とJSONを確認してください。"); }
        }
        var builders = new Dictionary<string, SongBuilder>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hash = NormalizeHash(row.Hash);
            if (hash is null)
            {
                unresolved.Add(new("SynthDB", null, row.Title, "leaderboard_hashが空または確認済み64桁hex形式ではありません。"));
                continue;
            }

            if (builders.ContainsKey(hash))
            {
                unresolved.Add(new("SynthDB", hash, row.Title, "同一hashに複数metadataがあるため統合しませんでした。"));
                continue;
            }

            var availability = customLocation?.Status switch
            {
                DataLocationStatus.Available => customFiles.Contains(Path.GetFileName(row.FileName)) ? SongAvailability.Available : SongAvailability.Missing,
                DataLocationStatus.Unavailable => SongAvailability.UnavailableBecauseParentLocationUnavailable,
                _ => SongAvailability.Unknown
            };
            builders[hash] = new(hash, SongKind.Custom, row.Title, row.Artist, row.Mapper, row.Bpm,
                row.DurationSeconds is >= 0 ? TimeSpan.FromSeconds(row.DurationSeconds.Value) : null,
                row.Created is null ? null : DateTimeOffset.FromUnixTimeSeconds(row.Created.Value), availability,
                covers.Resolve(hash), ResolveAudio(audioLocation, row.FileName), !string.IsNullOrWhiteSpace(row.Title) && !string.IsNullOrWhiteSpace(row.Artist));
        }

        var playlistMemberships = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var playlistSummaries = new List<PlaylistSummary>();
        var playlistEntryCount = 0;
        var playlistResolved = 0;
        ReadPlaylists(environment.Find(DataLocationKind.Playlists), warnings, unresolved, cancellationToken,
            (file, name, entries) =>
            {
                playlistSummaries.Add(new(file, name, entries.Count));
                foreach (var entry in entries)
                {
                    playlistEntryCount++;
                    var hash = NormalizeHash(entry.Hash);
                    if (hash is null)
                    {
                        unresolved.Add(new($"Playlist: {name}", entry.Hash, entry.Name, "確認済みhash形式ではありません。"));
                        continue;
                    }
                    if (!playlistMemberships.TryGetValue(hash, out var memberships)) playlistMemberships[hash] = memberships = [];
                    if (!memberships.Contains(name, StringComparer.OrdinalIgnoreCase)) memberships.Add(name);
                    if (builders.ContainsKey(hash)) playlistResolved++;
                    else
                    {
                        unresolved.Add(new($"Playlist: {name}", hash, entry.Name, "SynthDBと照合できないため公式側または未解決として保持しました。"));
                    }
                }
            });

        var favoriteHashes = ReadFavorites(environment.Find(DataLocationKind.Favorites), warnings, unresolved);
        var favoriteResolved = favoriteHashes.Count(hash => rows.Any(row => string.Equals(row.Hash, hash, StringComparison.OrdinalIgnoreCase)));
        var songs = builders.Values.Select(builder => builder.ToSong(
                favoriteHashes.Contains(builder.Hash),
                playlistMemberships.TryGetValue(builder.Hash, out var memberships) ? memberships : []) with
                {
                    FavoriteReference = builder.Kind == SongKind.Custom && builder.Complete ? $"{builder.Title}-{builder.Artist}-{builder.Hash}" : null,
                    FileName = rows.FirstOrDefault(r => string.Equals(r.Hash, builder.Hash, StringComparison.OrdinalIgnoreCase))?.FileName,
                    IsBlacklisted = rows.Any(r => string.Equals(r.Hash, builder.Hash, StringComparison.OrdinalIgnoreCase) && blacklist.Contains(r.FileName))
                })
            .OrderBy(song => song.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(song => song.Artist, StringComparer.CurrentCultureIgnoreCase).ToArray();

        var missing = songs.Count(x => x.Availability == SongAvailability.Missing);
        var unknown = songs.Count(x => x.Availability is SongAvailability.Unknown or SongAvailability.UnavailableBecauseParentLocationUnavailable);
        if (customLocation?.Status == DataLocationStatus.Unavailable)
            warnings.Add("Custom Songs保存先にアクセスできません。全曲をMissingとは判断せず、ゲームデータを変更していません。");
        var coverDiagnostics = covers.CreateDiagnostics(songs);
        return new(songs, playlistSummaries, unresolved, warnings,
            new(songs.Length, songs.Count(x => x.Identity.Kind == SongKind.Custom), songs.Count(x => x.Identity.Kind != SongKind.Custom),
                favoriteResolved, favoriteHashes.Count - favoriteResolved, playlistResolved, playlistEntryCount - playlistResolved, missing, unknown, coverDiagnostics));
    }

    private static string? RequireAvailable(EnvironmentDiscoveryResult environment, DataLocationKind kind) =>
        environment.Find(kind) is { Status: DataLocationStatus.Available, ResolvedPath: not null } location ? location.ResolvedPath : null;

    private static HashSet<string> ReadFileNames(DataLocation? location, string pattern, List<string> warnings)
    {
        if (location is not { Status: DataLocationStatus.Available, ResolvedPath: not null }) return new(StringComparer.OrdinalIgnoreCase);
        try { return Directory.EnumerateFiles(location.ResolvedPath, pattern, SearchOption.TopDirectoryOnly).Select(path => Path.GetFileName(path)!).ToHashSet(StringComparer.OrdinalIgnoreCase); }
        catch (Exception e) when (IsReadFailure(e)) { warnings.Add($"{location.Kind}の一覧を読めませんでした。保存先を確認してください。データは変更していません。"); return new(StringComparer.OrdinalIgnoreCase); }
    }

    private static CoverCatalog ReadCoverCatalog(DataLocation? location, List<string> warnings)
    {
        if (location is not { Status: DataLocationStatus.Available, ResolvedPath: not null })
        {
            if (location?.Status == DataLocationStatus.Unavailable)
                warnings.Add("ジャケット画像の保存先にアクセスできません。曲情報はそのまま表示し、ゲームデータは変更していません。");
            return CoverCatalog.Unavailable;
        }

        try
        {
            var candidates = Directory.EnumerateFiles(location.ResolvedPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var groups = candidates.GroupBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase).ToArray();
            var entries = new Dictionary<string, CoverEntry>(StringComparer.OrdinalIgnoreCase);
            var invalid = 0;
            var conflicts = 0;
            foreach (var group in groups)
            {
                if (group.Count() != 1)
                {
                    conflicts++;
                    continue;
                }

                var path = group.Single();
                var hash = NormalizeHash(group.Key);
                var png = InspectPng(path);
                if (!png.IsValid) invalid++;
                if (hash is not null) entries[hash] = new(path, png);
            }
            return new(candidates.Length, entries, invalid, conflicts, true);
        }
        catch (Exception e) when (IsReadFailure(e))
        {
            warnings.Add("ジャケット画像の保存先にアクセスできません。曲情報はそのまま表示し、ゲームデータは変更していません。");
            return CoverCatalog.Unavailable;
        }
    }

    private static PngInfo InspectPng(string path)
    {
        try
        {
            Span<byte> header = stackalloc byte[24];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length == 0 || stream.Read(header) != header.Length) return PngInfo.Invalid;
            ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
            if (!header[..8].SequenceEqual(signature) || !header[12..16].SequenceEqual("IHDR"u8)) return PngInfo.Invalid;
            var width = ReadBigEndianInt32(header[16..20]);
            var height = ReadBigEndianInt32(header[20..24]);
            return width > 0 && height > 0 ? new(true, width, height) : PngInfo.Invalid;
        }
        catch (Exception e) when (IsReadFailure(e)) { return PngInfo.Invalid; }
    }

    private static int ReadBigEndianInt32(ReadOnlySpan<byte> value) =>
        (value[0] << 24) | (value[1] << 16) | (value[2] << 8) | value[3];

    private static AudioResolution ResolveAudio(DataLocation? location, string fileName)
    {
        if (location is not { Status: DataLocationStatus.Available, ResolvedPath: not null })
            return location?.Status == DataLocationStatus.Unavailable
                ? new(AudioPreviewState.ParentLocationUnavailable, null)
                : new(AudioPreviewState.Unknown, null);

        try
        {
            var directory = Path.Combine(location.ResolvedPath, Path.GetFileName(fileName));
            if (!Directory.Exists(directory)) return AudioResolution.Missing;
            var candidates = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(Path.GetExtension(path), ".ogg", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (candidates.Length == 0) return AudioResolution.Missing;
            if (candidates.Length != 1 || !HasOggHeader(candidates[0])) return new(AudioPreviewState.Invalid, null);
            return new(AudioPreviewState.Available, candidates[0]);
        }
        catch (Exception e) when (IsReadFailure(e))
        {
            return new(AudioPreviewState.Unknown, null);
        }
    }

    private static bool HasOggHeader(string path)
    {
        Span<byte> header = stackalloc byte[4];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return stream.Read(header) == header.Length && header.SequenceEqual("OggS"u8);
    }

    private static IReadOnlyList<DbRow> ReadDatabase(string path, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Cache = SqliteCacheMode.Private, Pooling = false }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var schema = connection.CreateCommand())
        {
            schema.CommandText = "PRAGMA table_info(TracksCache)";
            using var reader = schema.ExecuteReader();
            while (reader.Read()) columns.Add(reader.GetString(1));
        }
        string[] required = ["file_name", "song_name", "author", "beatmapper", "bpm", "leaderboard_hash", "duration", "date_created"];
        if (required.Any(column => !columns.Contains(column))) throw new InvalidDataException("SynthDB TracksCache schemaが確認済み構造と一致しません。推測による読込は行いません。");

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT file_name, song_name, author, beatmapper, bpm, leaderboard_hash, duration, date_created FROM TracksCache";
        using var data = command.ExecuteReader();
        var rows = new List<DbRow>();
        while (data.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(new(Text(data, 0), Text(data, 1), Text(data, 2), Text(data, 3), Number(data, 4), Text(data, 5), Number(data, 6), Integer(data, 7)));
        }
        return rows;
    }

    private static HashSet<string> ReadFavorites(DataLocation? location, List<string> warnings, List<UnresolvedLibraryEntry> unresolved)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (location is not { Status: DataLocationStatus.Available, ResolvedPath: not null }) return result;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(location.ResolvedPath));
            foreach (var item in doc.RootElement.GetProperty("Favorite").EnumerateArray())
            {
                var raw = item.GetString();
                var match = raw is null ? Match.Empty : FavoriteHashRegex().Match(raw);
                if (match.Success) result.Add(match.Groups[1].Value.ToLowerInvariant());
                else unresolved.Add(new("Favorites", null, raw, "末尾64桁hashを確認できないため保持しました。"));
            }
        }
        catch (Exception e) when (IsReadFailure(e) || e is JsonException or KeyNotFoundException or InvalidOperationException)
        { warnings.Add("Favoritesを解析できませんでした。Favorite状態は未確定のまま、ファイルを変更していません。"); }
        return result;
    }

    private static void ReadPlaylists(DataLocation? location, List<string> warnings, List<UnresolvedLibraryEntry> unresolved, CancellationToken cancellationToken, Action<string, string, List<PlaylistEntry>> accept)
    {
        if (location is not { Status: DataLocationStatus.Available, ResolvedPath: not null }) return;
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(location.ResolvedPath, "*.playlist", SearchOption.TopDirectoryOnly).ToArray(); }
        catch (Exception e) when (IsReadFailure(e)) { warnings.Add("Playlist保存先を列挙できませんでした。データは変更していません。"); return; }
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                var name = root.TryGetProperty("namePlaylist", out var nameNode) ? nameNode.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || !root.TryGetProperty("dataString", out var data) || data.ValueKind != JsonValueKind.Array) throw new JsonException();
                var entries = new List<PlaylistEntry>();
                foreach (var entry in data.EnumerateArray()) entries.Add(new(
                    JsonText(entry, "hash"), JsonText(entry, "name"), JsonText(entry, "author"), JsonText(entry, "beatmapper"), JsonNumber(entry, "trackDuration")));
                accept(Path.GetFileName(file), name, entries);
            }
            catch (Exception e) when (IsReadFailure(e) || e is JsonException or InvalidOperationException)
            {
                warnings.Add($"Playlist「{Path.GetFileName(file)}」を解析できませんでした。他のPlaylistは読み込み、対象ファイルは変更していません。");
                unresolved.Add(new($"Playlist file: {Path.GetFileName(file)}", null, null, "JSON parse/validation failure"));
            }
        }
    }

    private static string? NormalizeHash(string? value) => value is not null && HashRegex().IsMatch(value) ? value.ToLowerInvariant() : null;
    private static string Text(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? "";
    private static double? Number(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    private static long? Integer(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    private static string JsonText(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static double? JsonNumber(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.TryGetDouble(out var result) ? result : null;
    private static bool IsReadFailure(Exception e) => e is IOException or UnauthorizedAccessException or DirectoryNotFoundException or FileNotFoundException or DriveNotFoundException or PathTooLongException or ArgumentException or NotSupportedException;

    private sealed record DbRow(string FileName, string Title, string Artist, string Mapper, double? Bpm, string Hash, double? DurationSeconds, long? Created);
    private sealed record PlaylistEntry(string Hash, string Name, string Artist, string Mapper, double? DurationSeconds);
    private sealed record SongBuilder(string Hash, SongKind Kind, string Title, string Artist, string Mapper, double? Bpm, TimeSpan? Duration, DateTimeOffset? AddedAt, SongAvailability Availability, CoverResolution Cover, AudioResolution Audio, bool Complete)
    {
        public Song ToSong(bool favorite, IReadOnlyList<string> memberships) => new(new(Kind, Hash), string.IsNullOrWhiteSpace(Title) ? "（タイトル不明）" : Title,
            string.IsNullOrWhiteSpace(Artist) ? "（アーティスト不明）" : Artist, Mapper, Bpm, Duration, "Unknown", favorite, memberships, AddedAt, Hash, Availability, Complete,
            Cover.State == CoverArtState.Available, Audio.State == AudioPreviewState.Available, Cover.State, Cover.Path, Cover.Key, Audio.State, Audio.Path);
    }

    private sealed record AudioResolution(AudioPreviewState State, string? Path)
    {
        public static AudioResolution Missing { get; } = new(AudioPreviewState.Missing, null);
    }

    private sealed record PngInfo(bool IsValid, int Width, int Height)
    {
        public static PngInfo Invalid { get; } = new(false, 0, 0);
    }
    private sealed record CoverEntry(string Path, PngInfo Image);
    private sealed record CoverResolution(CoverArtState State, string? Path, string? Key)
    {
        public static CoverResolution Missing(string key) => new(CoverArtState.Missing, null, key);
    }
    private sealed class CoverCatalog(int fileCount, Dictionary<string, CoverEntry> entries, int invalidCount, int conflictCount, bool available)
    {
        public static CoverCatalog Unavailable { get; } = new(0, new(StringComparer.OrdinalIgnoreCase), 0, 0, false);
        public CoverResolution Resolve(string hash)
        {
            if (!available) return new(CoverArtState.ParentLocationUnavailable, null, hash);
            if (!entries.TryGetValue(hash, out var entry)) return CoverResolution.Missing(hash);
            return entry.Image.IsValid
                ? new(CoverArtState.Available, entry.Path, hash)
                : new(CoverArtState.InvalidImage, null, hash);
        }
        public CoverArtDiagnostics CreateDiagnostics(IReadOnlyList<Song> songs)
        {
            var custom = songs.Where(song => song.Identity.Kind == SongKind.Custom).ToArray();
            var resolved = custom.Count(song => song.CoverState == CoverArtState.Available);
            var matchedKeys = custom.Select(song => song.CoverKey).Where(key => key is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var validEntries = entries.Where(pair => pair.Value.Image.IsValid).ToArray();
            var sizes = validEntries.Select(pair => pair.Value.Image).ToArray();
            var dimensions = sizes.Length == 0 ? "n/a" : $"{sizes.Min(x => x.Width)}x{sizes.Min(x => x.Height)}–{sizes.Max(x => x.Width)}x{sizes.Max(x => x.Height)}; square {sizes.Count(x => x.Width == x.Height)}/{sizes.Length}";
            return new(fileCount, validEntries.Length, invalidCount, entries.Count, entries.Keys.Count(matchedKeys.Contains), resolved,
                custom.Length - resolved, 0, songs.Count(song => song.Identity.Kind != SongKind.Custom),
                entries.Keys.Count(key => !matchedKeys.Contains(key)), conflictCount, "PNG", dimensions, available);
        }
    }

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)] private static partial Regex HashRegex();
    [GeneratedRegex("([0-9a-fA-F]{64})$", RegexOptions.CultureInvariant)] private static partial Regex FavoriteHashRegex();
}
