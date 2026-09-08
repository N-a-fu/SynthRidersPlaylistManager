using SynthRidersPlaylistManager.App.Services;
using SynthRidersPlaylistManager.App.ViewModels;
using System.IO;

namespace SynthRidersPlaylistManager.Tests;

public sealed class VolumePersistenceTests
{
    [Fact]
    public void DefaultsToFiftyAndRestoresSavedVolume()
    {
        var directory = Path.Combine(Path.GetTempPath(), "srpm-volume-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "ui-settings.json");
        try
        {
            var store = new UiSettingsStore(path);
            using (var first = new MainViewModel(null, null, null, null, new FakeAudioPreviewPlayer(), uiSettingsStore: store))
            {
                Assert.Equal(50, first.Volume);
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"Language\":\"ja-JP\"}");
            using (var legacySettings = new MainViewModel(null, null, null, null, new FakeAudioPreviewPlayer(), uiSettingsStore: store))
            {
                Assert.Equal(50, legacySettings.Volume);
                legacySettings.Volume = 27;
                Assert.Equal(27, store.Load().Volume);
            }

            using var restarted = new MainViewModel(null, null, null, null, new FakeAudioPreviewPlayer(), uiSettingsStore: store);
            Assert.Equal(27, restarted.Volume);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private sealed class FakeAudioPreviewPlayer : IAudioPreviewPlayer
    {
        public event EventHandler? PlaybackStateChanged;
        public bool IsPlaying => false;
        public TimeSpan Position { get; set; }
        public TimeSpan Duration => TimeSpan.Zero;
        public float Volume { get; set; }
        public void Load(string path) { }
        public void Play() => PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        public void Pause() { }
        public void Seek(TimeSpan position) => Position = position;
        public void Stop() { }
        public void Dispose() { }
    }
}
