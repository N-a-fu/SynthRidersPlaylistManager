namespace SynthRidersPlaylistManager.Infrastructure.EnvironmentDiscovery;

public interface IFileSystemProbe
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    bool IsStorageAvailable(string path);
    IEnumerable<string> EnumerateFiles(string path, string pattern);
    string ReadAllText(string path);
    byte[] ReadPrefix(string path, int length);
}

public sealed class PhysicalFileSystemProbe : IFileSystemProbe
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);
    public bool IsStorageAvailable(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(root)) return false;
        try { return new DriveInfo(root).IsReady; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }
    public IEnumerable<string> EnumerateFiles(string path, string pattern) => Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public byte[] ReadPrefix(string path, int length)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buffer = new byte[length];
        var read = stream.Read(buffer, 0, buffer.Length);
        return buffer[..read];
    }
}
