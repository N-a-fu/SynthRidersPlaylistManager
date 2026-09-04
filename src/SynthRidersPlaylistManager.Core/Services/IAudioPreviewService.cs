namespace SynthRidersPlaylistManager.Core.Services;

public interface IAudioPreviewService
{
    bool IsAvailable { get; }

    bool IsPlaying { get; }

    void Play();

    void Pause();
}
