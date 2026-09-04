using System.Text;
using System.Text.Json;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Infrastructure.EnvironmentDiscovery;

public sealed class DataLocationValidator(IFileSystemProbe fileSystem)
{
    public DataLocation Validate(DataLocationKind kind, string path, DataLocationSource source, bool isUserOverride)
    {
        var now = DateTimeOffset.Now;
        if (string.IsNullOrWhiteSpace(path)) return new(kind, null, source, DataLocationStatus.NotConfigured, now, "保存先が設定されていません。", isUserOverride);

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!fileSystem.IsStorageAvailable(fullPath))
                return new(kind, fullPath, source, DataLocationStatus.Unavailable, now, UnavailableMessage(kind), isUserOverride);

            var expectsFile = kind is DataLocationKind.Favorites or DataLocationKind.SynthDatabase;
            if (expectsFile && !fileSystem.FileExists(fullPath))
                return new(kind, fullPath, source, DataLocationStatus.Missing, now, "保存先ストレージにはアクセスできますが、対象ファイルが見つかりません。データ変更は行っていません。", isUserOverride);
            if (!expectsFile && !fileSystem.DirectoryExists(fullPath))
                return new(kind, fullPath, source, DataLocationStatus.Missing, now, "保存先ストレージにはアクセスできますが、対象フォルダが見つかりません。データ変更は行っていません。", isUserOverride);

            return kind switch
            {
                DataLocationKind.GameRoot => ValidateGameRoot(fullPath, source, isUserOverride, now),
                DataLocationKind.Playlists => ValidateDirectoryContent(kind, fullPath, "*.playlist", source, isUserOverride, now),
                DataLocationKind.Favorites => ValidateFavorites(fullPath, source, isUserOverride, now),
                DataLocationKind.CustomSongs => ValidateDirectoryContent(kind, fullPath, "*.synth", source, isUserOverride, now),
                DataLocationKind.SynthDatabase => ValidateSynthDatabase(fullPath, source, isUserOverride, now),
                DataLocationKind.ImagesCache => ValidateDirectoryContent(kind, fullPath, "*.png", source, isUserOverride, now),
                DataLocationKind.TempAudio => new(kind, fullPath, source, DataLocationStatus.Available, now, "フォルダへアクセスできます。tempExtのlifecycleは未確認のためSource of Truthには使用しません。", isUserOverride),
                _ => new(kind, fullPath, source, DataLocationStatus.Unknown, now, "完全な検証方法が未確定です。データ変更は行いません。", isUserOverride)
            };
        }
        catch (Exception e) when (IsIoFailure(e))
        {
            return new(kind, path, source, DataLocationStatus.Unavailable, now, $"アクセス中に{FriendlyFailure(e)}が発生しました。データが削除されたとは判断せず、変更も行っていません。", isUserOverride);
        }
    }

    private DataLocation ValidateGameRoot(string path, DataLocationSource source, bool manual, DateTimeOffset now)
    {
        var exe = Path.Combine(path, "SynthRiders.exe");
        var data = Path.Combine(path, "SynthRiders_Data");
        return fileSystem.FileExists(exe) && fileSystem.DirectoryExists(data)
            ? new(DataLocationKind.GameRoot, path, source, DataLocationStatus.Available, now, "Steam版Synth Ridersを識別する実行ファイルとDataフォルダを確認しました。", manual)
            : new(DataLocationKind.GameRoot, path, source, DataLocationStatus.Invalid, now, "SynthRiders.exeとSynthRiders_Dataを確認できないため、ゲームフォルダとして採用しません。", manual);
    }

    private DataLocation ValidateDirectoryContent(DataLocationKind kind, string path, string pattern, DataLocationSource source, bool manual, DateTimeOffset now)
    {
        using var enumerator = fileSystem.EnumerateFiles(path, pattern).GetEnumerator();
        return enumerator.MoveNext()
            ? new(kind, path, source, DataLocationStatus.Available, now, $"アクセス可能で、{pattern}を確認しました。", manual)
            : new(kind, path, source, DataLocationStatus.Unknown, now, $"フォルダへアクセスできますが{pattern}はありません。空フォルダの可能性があるためInvalidとは判断しません。", manual);
    }

    private DataLocation ValidateFavorites(string path, DataLocationSource source, bool manual, DateTimeOffset now)
    {
        try
        {
            using var document = JsonDocument.Parse(fileSystem.ReadAllText(path));
            var valid = document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("Favorite", out var favorite) && favorite.ValueKind == JsonValueKind.Array;
            return valid
                ? new(DataLocationKind.Favorites, path, source, DataLocationStatus.Available, now, "UTF-8 JSONのFavorite配列を読み取り専用で確認しました。", manual)
                : new(DataLocationKind.Favorites, path, source, DataLocationStatus.Invalid, now, "確認済みのFavorites JSON構造ではありません。ファイルは変更していません。", manual);
        }
        catch (JsonException)
        {
            return new(DataLocationKind.Favorites, path, source, DataLocationStatus.Invalid, now, "JSONとして解析できないためFavoritesとして採用しません。ファイルは変更していません。", manual);
        }
    }

    private DataLocation ValidateSynthDatabase(string path, DataLocationSource source, bool manual, DateTimeOffset now)
    {
        var prefix = fileSystem.ReadPrefix(path, 16);
        var valid = prefix.Length == 16 && Encoding.ASCII.GetString(prefix) == "SQLite format 3\0";
        return valid
            ? new(DataLocationKind.SynthDatabase, path, source, DataLocationStatus.Available, now, "SQLite 3 headerを読み取り専用で確認しました。", manual)
            : new(DataLocationKind.SynthDatabase, path, source, DataLocationStatus.Invalid, now, "SQLite 3として確認できないためSynthDBとして採用しません。", manual);
    }

    private static bool IsIoFailure(Exception e) => e is IOException or UnauthorizedAccessException or DirectoryNotFoundException or FileNotFoundException or DriveNotFoundException or PathTooLongException or ArgumentException or NotSupportedException or JsonException;
    private static string FriendlyFailure(Exception e) => e switch { UnauthorizedAccessException => "アクセス権エラー", DriveNotFoundException => "ドライブ切断", PathTooLongException => "長すぎるPath", JsonException => "形式検証エラー", _ => "I/Oエラー" };
    private static string UnavailableMessage(DataLocationKind kind) => $"{kind}の保存先にアクセスできません。外付けドライブの接続や保存先変更を確認してください。データが削除されたとは判断せず、PlaylistやFavoriteも変更していません。";
}
