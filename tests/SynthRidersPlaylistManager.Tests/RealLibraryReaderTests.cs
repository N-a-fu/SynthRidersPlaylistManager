using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Infrastructure.GameData;

namespace SynthRidersPlaylistManager.Tests;

public sealed class RealLibraryReaderTests
{
    [Fact]
    public async Task LoadsUnicodeMetadataAndKeepsDuplicateTitlesSeparateByHash()
    {
        using var fixture = new LibraryFixture();
        fixture.AddTrack('a', "同じ曲", "作者一", "Mapper α", "one.synth");
        fixture.AddTrack('b', "同じ曲", "作者二", "Mapper β", "two.synth");
        fixture.WriteFavorites(fixture.Hash('a'));
        fixture.WritePlaylist("テスト", (fixture.Hash('a'), "同じ曲", "作者一", "Mapper α"));

        var snapshot = await fixture.Reader.LoadAsync(fixture.Environment());

        Assert.Equal(2, snapshot.Songs.Count);
        Assert.Equal(2, snapshot.Songs.Select(x => x.Identity).Distinct().Count());
        Assert.Contains(snapshot.Songs, x => x.Artist == "作者一" && x.IsFavorite && x.PlaylistNames.Contains("テスト"));
        Assert.Equal(2, snapshot.Diagnostics.CustomSongs);
    }

    [Fact]
    public async Task KeepsUnresolvedPlaylistRecordsOnDiskWithoutMaterializingThemInLibrary()
    {
        using var fixture = new LibraryFixture();
        fixture.AddTrack('a', "Custom", "Artist", "Mapper", "one.synth");
        var officialHash = fixture.Hash('f');
        fixture.WriteFavorites(fixture.Hash('a'), officialHash);
        fixture.WritePlaylist("mixed", (fixture.Hash('a'), "Custom", "Artist", "Mapper"), (officialHash, "Official Sample", "Built-in Artist", ""));
        var playlistPath = Path.Combine(fixture.Playlists, "mixed.playlist");
        var playlistBefore = File.ReadAllBytes(playlistPath);

        var snapshot = await fixture.Reader.LoadAsync(fixture.Environment());

        var custom = Assert.Single(snapshot.Songs);
        Assert.Equal(SongKind.Custom, custom.Identity.Kind);
        Assert.Contains("mixed", custom.PlaylistNames);
        Assert.Equal(1, snapshot.Diagnostics.FavoritesResolved);
        Assert.Equal(1, snapshot.Diagnostics.FavoritesUnresolved);
        Assert.Equal(1, snapshot.Diagnostics.PlaylistEntriesResolved);
        Assert.Equal(1, snapshot.Diagnostics.PlaylistEntriesUnresolved);
        Assert.Contains(snapshot.UnresolvedEntries, x => x.Hash == officialHash);
        Assert.DoesNotContain(snapshot.Songs, x => x.Identity.StableId == officialHash);
        Assert.Equal(playlistBefore, File.ReadAllBytes(playlistPath));
    }

    [Fact]
    public async Task BrokenPlaylistIsIsolatedAndUnavailableParentDoesNotCreateMissingSongs()
    {
        using var fixture = new LibraryFixture();
        fixture.AddTrack('a', "Song", "Artist", "Mapper", "one.synth");
        File.WriteAllText(Path.Combine(fixture.Playlists, "broken.playlist"), "not-json");
        fixture.WritePlaylist("valid", (fixture.Hash('a'), "Song", "Artist", "Mapper"));

        var environment = fixture.Environment(customStatus: DataLocationStatus.Unavailable);
        var snapshot = await fixture.Reader.LoadAsync(environment);

        Assert.Single(snapshot.Playlists);
        Assert.Equal(SongAvailability.UnavailableBecauseParentLocationUnavailable, Assert.Single(snapshot.Songs).Availability);
        Assert.Equal(0, snapshot.Diagnostics.MissingSongs);
        Assert.NotEmpty(snapshot.Warnings);
    }

    [Fact]
    public async Task ExactHashPngMapsCoverAndDuplicateTitleDoesNotMixImages()
    {
        using var fixture = new LibraryFixture();
        fixture.AddTrack('a', "Duplicate", "Artist One", "Mapper One", "one.synth");
        fixture.AddTrack('b', "Duplicate", "Artist Two", "Mapper Two", "two.synth");
        fixture.WritePngHeader(fixture.Hash('a'), 512, 512);

        var snapshot = await fixture.Reader.LoadAsync(fixture.Environment());

        var covered = Assert.Single(snapshot.Songs, song => song.Identity.StableId == fixture.Hash('a'));
        var missing = Assert.Single(snapshot.Songs, song => song.Identity.StableId == fixture.Hash('b'));
        Assert.Equal(CoverArtState.Available, covered.CoverState);
        Assert.EndsWith($"{fixture.Hash('a')}.png", covered.CoverImagePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CoverArtState.Missing, missing.CoverState);
        Assert.Null(missing.CoverImagePath);
        Assert.Equal(1, snapshot.Diagnostics.Covers?.ResolvedSongs);
        Assert.Equal(1, snapshot.Diagnostics.Covers?.MissingSongs);
    }

    [Fact]
    public async Task InvalidImageUsesPlaceholderStateWithoutFailingLibrary()
    {
        using var fixture = new LibraryFixture();
        fixture.AddTrack('a', "Song", "Artist", "Mapper", "one.synth");
        File.WriteAllText(Path.Combine(fixture.Images, $"{fixture.Hash('a')}.png"), "not-png");

        var snapshot = await fixture.Reader.LoadAsync(fixture.Environment());

        Assert.Equal(CoverArtState.InvalidImage, Assert.Single(snapshot.Songs).CoverState);
        Assert.Equal(1, snapshot.Diagnostics.Covers?.InvalidImages);
        Assert.Empty(snapshot.Warnings);
    }

    [Fact]
    public async Task UnavailableImagesCacheKeepsRealSongsAndUsesParentUnavailableState()
    {
        using var fixture = new LibraryFixture();
        fixture.AddTrack('a', "Song", "Artist", "Mapper", "one.synth");

        var snapshot = await fixture.Reader.LoadAsync(fixture.Environment(imageStatus: DataLocationStatus.Unavailable));

        Assert.Single(snapshot.Songs);
        Assert.Equal(CoverArtState.ParentLocationUnavailable, snapshot.Songs[0].CoverState);
        Assert.False(snapshot.Diagnostics.Covers?.LocationAvailable);
        Assert.Single(snapshot.Warnings);
    }

    [Fact]
    public async Task UnresolvedPlaylistEntryIsNotMaterializedAndItsImageRemainsOrphaned()
    {
        using var fixture = new LibraryFixture();
        fixture.AddTrack('a', "Custom", "Artist", "Mapper", "one.synth");
        var officialHash = fixture.Hash('f');
        fixture.WritePngHeader(officialHash, 256, 256);
        fixture.WritePlaylist("mixed", (officialHash, "Official", "Built-in", ""));

        var snapshot = await fixture.Reader.LoadAsync(fixture.Environment());

        Assert.Single(snapshot.Songs);
        Assert.DoesNotContain(snapshot.Songs, song => song.Identity.StableId == officialHash);
        Assert.Equal(0, snapshot.Diagnostics.Covers?.OfficialSideUnresolved);
        Assert.Equal(1, snapshot.Diagnostics.Covers?.OrphanImages);
    }

    [Fact]
    public async Task ExactSynthDbFileNameMapsValidTempAudioWithoutAffectingSongAvailability()
    {
        using var fixture = new LibraryFixture();
        fixture.AddTrack('a', "Preview", "Artist", "Mapper", "preview.synth");
        fixture.WriteOgg("preview.synth", "audio.ogg", valid: true);

        var song = Assert.Single((await fixture.Reader.LoadAsync(fixture.Environment())).Songs);

        Assert.Equal(AudioPreviewState.Available, song.AudioState);
        Assert.True(song.HasAudio);
        Assert.EndsWith("audio.ogg", song.AudioPreviewPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SongAvailability.Available, song.Availability);
    }

    [Fact]
    public async Task MissingOrInvalidTempAudioIsOptionalMetadataNotSongMissing()
    {
        using var fixture = new LibraryFixture();
        fixture.AddTrack('a', "Missing audio", "Artist", "Mapper", "missing.synth");
        fixture.AddTrack('b', "Invalid audio", "Artist", "Mapper", "invalid.synth");
        fixture.WriteOgg("invalid.synth", "audio.ogg", valid: false);

        var songs = (await fixture.Reader.LoadAsync(fixture.Environment())).Songs;

        Assert.Equal(AudioPreviewState.Missing, Assert.Single(songs, x => x.Title == "Missing audio").AudioState);
        Assert.Equal(AudioPreviewState.Invalid, Assert.Single(songs, x => x.Title == "Invalid audio").AudioState);
        Assert.All(songs, song => Assert.Equal(SongAvailability.Available, song.Availability));
    }

    [Fact]
    public async Task BlacklistMapsExactSynthDbFileNameAndDoesNotAffectFavoriteOrAvailability()
    {
        using var fixture = new LibraryFixture();
        fixture.AddTrack('a', "Blocked", "Artist", "Mapper", "blocked.synth");
        fixture.AddTrack('b', "Other", "Artist", "Mapper", "other.synth");
        fixture.WriteFavorites(fixture.Hash('a'));
        File.WriteAllText(Path.Combine(fixture.Root, "twitchsettings.bin"), "{\"Blacklist\":[\"blocked.synth\",\"OTHER.synth\"]}");
        var environment = fixture.Environment();
        environment = environment with { Locations = environment.Locations.Append(new DataLocation(DataLocationKind.GameRoot, fixture.Root, DataLocationSource.Derived, DataLocationStatus.Available, DateTimeOffset.Now, "fixture", false)).ToArray() };
        var songs = (await fixture.Reader.LoadAsync(environment)).Songs;
        var blocked = Assert.Single(songs, x => x.IsBlacklisted);
        Assert.Equal("blocked.synth", blocked.FileName);
        Assert.Equal("Blocked-Artist-" + fixture.Hash('a'), blocked.FavoriteReference);
        Assert.True(blocked.IsFavorite);
        Assert.Equal(SongAvailability.Available, blocked.Availability);
        Assert.False(Assert.Single(songs, x => x.Title == "Other").IsBlacklisted);
    }

    private sealed class LibraryFixture : IDisposable
    {
        private readonly SqliteConnection _writeConnection;
        public LibraryFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"srpm-phase2b-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            CustomSongs = Directory.CreateDirectory(Path.Combine(Root, "CustomSongs")).FullName;
            Playlists = Directory.CreateDirectory(Path.Combine(Root, "Playlist")).FullName;
            Images = Directory.CreateDirectory(Path.Combine(Root, "ImagesCache")).FullName;
            TempAudio = Directory.CreateDirectory(Path.Combine(Root, "tempExt")).FullName;
            Favorites = Path.Combine(Root, "favorites.bin");
            Database = Path.Combine(Root, "SynthDB");
            _writeConnection = new(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString());
            _writeConnection.Open();
            using var command = _writeConnection.CreateCommand();
            command.CommandText = "CREATE TABLE TracksCache (id INTEGER PRIMARY KEY, file_name TEXT, song_name TEXT, author TEXT, beatmapper TEXT, bpm NUMERIC, image_file TEXT, leaderboard_hash TEXT, notes_count TEXT, duration NUMERIC, date_created INTEGER)";
            command.ExecuteNonQuery();
            WriteFavorites();
        }
        public string Root { get; }
        public string CustomSongs { get; }
        public string Playlists { get; }
        public string Images { get; }
        public string TempAudio { get; }
        public string Favorites { get; }
        public string Database { get; }
        public RealLibraryReader Reader { get; } = new();
        public string Hash(char value) => new(value, 64);
        public void AddTrack(char hash, string title, string artist, string mapper, string fileName)
        {
            File.WriteAllText(Path.Combine(CustomSongs, fileName), "fixture");
            using var command = _writeConnection.CreateCommand();
            command.CommandText = "INSERT INTO TracksCache(file_name,song_name,author,beatmapper,bpm,image_file,leaderboard_hash,notes_count,duration,date_created) VALUES($file,$title,$artist,$mapper,120,'',$hash,'',180,1700000000)";
            command.Parameters.AddWithValue("$file", fileName); command.Parameters.AddWithValue("$title", title); command.Parameters.AddWithValue("$artist", artist); command.Parameters.AddWithValue("$mapper", mapper); command.Parameters.AddWithValue("$hash", Hash(hash));
            command.ExecuteNonQuery();
        }
        public void WriteFavorites(params string[] hashes) => File.WriteAllText(Favorites, JsonSerializer.Serialize(new { Favorite = hashes.Select(x => $"title-artist-{x}").ToArray() }));
        public void WritePngHeader(string hash, int width, int height)
        {
            var bytes = new byte[24];
            byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
            signature.CopyTo(bytes, 0);
            bytes[12] = (byte)'I'; bytes[13] = (byte)'H'; bytes[14] = (byte)'D'; bytes[15] = (byte)'R';
            WriteBigEndian(bytes, 16, width); WriteBigEndian(bytes, 20, height);
            File.WriteAllBytes(Path.Combine(Images, $"{hash}.png"), bytes);
        }
        public void WritePlaylist(string name, params (string Hash, string Name, string Artist, string Mapper)[] entries)
        {
            var payload = new { namePlaylist = name, dataString = entries.Select(x => new { hash = x.Hash, name = x.Name, author = x.Artist, beatmapper = x.Mapper, trackDuration = 180 }).ToArray() };
            File.WriteAllText(Path.Combine(Playlists, $"{name}.playlist"), JsonSerializer.Serialize(payload));
        }
        public void WriteOgg(string synthFileName, string oggFileName, bool valid)
        {
            var directory = Directory.CreateDirectory(Path.Combine(TempAudio, synthFileName)).FullName;
            File.WriteAllBytes(Path.Combine(directory, oggFileName), valid ? "OggSfixture"u8.ToArray() : "bad!"u8.ToArray());
        }
        public EnvironmentDiscoveryResult Environment(DataLocationStatus customStatus = DataLocationStatus.Available, DataLocationStatus imageStatus = DataLocationStatus.Available)
        {
            var now = DateTimeOffset.Now;
            DataLocation At(DataLocationKind kind, string path, DataLocationStatus status = DataLocationStatus.Available) => new(kind, path, DataLocationSource.Derived, status, now, "fixture", false);
            return new([At(DataLocationKind.SynthDatabase, Database), At(DataLocationKind.CustomSongs, CustomSongs, customStatus), At(DataLocationKind.Playlists, Playlists), At(DataLocationKind.Favorites, Favorites), At(DataLocationKind.ImagesCache, Images, imageStatus), At(DataLocationKind.TempAudio, TempAudio)], "fixture", now);
        }
        private static void WriteBigEndian(byte[] bytes, int offset, int value) { bytes[offset] = (byte)(value >> 24); bytes[offset + 1] = (byte)(value >> 16); bytes[offset + 2] = (byte)(value >> 8); bytes[offset + 3] = (byte)value; }
        public void Dispose() { _writeConnection.Dispose(); Directory.Delete(Root, true); }
    }
}
