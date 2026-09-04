namespace SynthRidersPlaylistManager.App.Services;

public interface IAudioPreviewPlayer : IDisposable
{
    event EventHandler? PlaybackStateChanged;
    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    float Volume { get; set; }
    void Load(string path);
    void Play();
    void Pause();
    void Seek(TimeSpan position);
    void Stop();
}
