using System.Text;
using System.Text.Json.Nodes;
using System.IO;
using SynthRidersPlaylistManager.Core.Models;
using SynthRidersPlaylistManager.Infrastructure.GameData;

namespace SynthRidersPlaylistManager.Tests;

public sealed class PlaylistStoreTests
{
    [Fact]
    public void CreateRenameAddRemoveAndDeleteUseSafeRealFiles()
    {
        using var fixture = new PlaylistFixture();
        var store = new PlaylistStore(() => true);

        var created = store.Create(fixture.Directory, "SPM TEST C");
        Assert.Equal("000006__spmtestc.playlist", created.FileName);
        Assert.Empty(Data(fixture.Path(created.FileName)));
        var withUnknown = Root(fixture.Path(created.FileName));
        withUnknown["unknownField"] = "kept";
        File.WriteAllText(fixture.Path(created.FileName), withUnknown.ToJsonString(), new UTF8Encoding(false));

        var songA = Song('a', "First");
        var songB = Song('b', "Second");
        var songC = Song('c', "Third");
        store.AddSongs(fixture.Directory, created.FileName, [songA, songB, songC]);
        store.AddSongs(fixture.Directory, created.FileName, [songA]);
        Assert.Equal([songA.Hash, songB.Hash, songC.Hash], Data(fixture.Path(created.FileName)).Select(NodeHash));

        var renamed = store.Rename(fixture.Directory, created.FileName, "Renamed Mix");
        Assert.Equal("000006__renamedmix.playlist", renamed.FileName);
        var renamedRoot = Root(fixture.Path(renamed.FileName));
        Assert.Equal("Renamed Mix", renamedRoot["namePlaylist"]!.GetValue<string>());

        store.RemoveSongs(fixture.Directory, renamed.FileName, [songA.Hash, songB.Hash]);
        Assert.Equal([songC.Hash], Data(fixture.Path(renamed.FileName)).Select(NodeHash));
        Assert.Equal("kept", Root(fixture.Path(renamed.FileName))["unknownField"]!.GetValue<string>());
        Assert.NotEmpty(System.IO.Directory.EnumerateFiles(fixture.Directory, "*.bak"));

        store.Delete(fixture.Directory, renamed.FileName);
        Assert.False(File.Exists(fixture.Path(renamed.FileName)));
        Assert.NotEmpty(System.IO.Directory.EnumerateFiles(fixture.Directory, "*.bak"));
    }

    [Fact]
    public void GameRunningBlocksEveryWrite()
    {
        using var fixture = new PlaylistFixture();
        var store = new PlaylistStore(() => false);
        Assert.Throws<InvalidOperationException>(() => store.Create(fixture.Directory, "Blocked"));
        Assert.Throws<InvalidOperationException>(() => store.AddSongs(fixture.Directory, "000005__existing.playlist", [Song('a', "A")]));
        Assert.Throws<InvalidOperationException>(() => store.RemoveSongs(fixture.Directory, "000005__existing.playlist", [new string('a', 64)]));
        Assert.Throws<InvalidOperationException>(() => store.Rename(fixture.Directory, "000005__existing.playlist", "Blocked"));
        Assert.Throws<InvalidOperationException>(() => store.Delete(fixture.Directory, "000005__existing.playlist"));
    }

    private static PlaylistWriteSong Song(char hash, string title) => new(new string(hash, 64), title, "Artist", "Mapper", 123);
    private static JsonObject Root(string path) => JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
    private static JsonArray Data(string path) => Root(path)["dataString"]!.AsArray();
    private static string NodeHash(JsonNode? node) => node!["hash"]!.GetValue<string>();

    private sealed class PlaylistFixture : IDisposable
    {
        public PlaylistFixture()
        {
            Directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "srpm-playlist-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            Write("000004__old.playlist", "Old", false);
            Write("000005__existing.playlist", "Existing", true);
        }
        public string Directory { get; }
        public string Path(string fileName) => System.IO.Path.Combine(Directory, fileName);
        private void Write(string fileName, string name, bool unknown)
        {
            var root = new JsonObject { ["dataString"] = new JsonArray(), ["SelectedIconIndex"] = 0, ["SelectedTexture"] = 0, ["namePlaylist"] = name, ["description"] = "New Playlist", ["gradientTop"] = "#000000", ["gradientDown"] = "#000000", ["colorTitle"] = "#FFFFFF", ["colorTexture"] = "#FFFFFF", ["creationDate"] = "1" };
            if (unknown) root["unknownField"] = "kept";
            File.WriteAllText(Path(fileName), root.ToJsonString(), new UTF8Encoding(false));
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
