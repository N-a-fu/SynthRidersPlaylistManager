using NAudio.Vorbis;
using NAudio.Wave;

namespace SynthRidersPlaylistManager.App.Services;

public sealed class AudioPreviewPlayer : IAudioPreviewPlayer
{
    private WaveOutEvent? _output;
    private VorbisWaveReader? _reader;
    private float _volume = 0.68f;

    public event EventHandler? PlaybackStateChanged;
    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;
    public TimeSpan Position => _reader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 1);
            if (_output is not null) _output.Volume = _volume;
        }
    }

    public void Load(string path)
    {
        Stop();
        _reader = new VorbisWaveReader(path);
        _output = new WaveOutEvent { Volume = _volume };
        _output.PlaybackStopped += OnPlaybackStopped;
        _output.Init(_reader);
    }

    public void Play()
    {
        if (_output is null) throw new InvalidOperationException("Audio preview is not loaded.");
        _output.Play();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        _output?.Pause();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(TimeSpan position)
    {
        if (_reader is null) return;
        _reader.CurrentTime = position < TimeSpan.Zero ? TimeSpan.Zero : position > _reader.TotalTime ? _reader.TotalTime : position;
    }

    public void Stop()
    {
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Stop();
            _output.Dispose();
            _output = null;
        }
        _reader?.Dispose();
        _reader = null;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e) => PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    public void Dispose() => Stop();
}
