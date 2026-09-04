using SynthRidersPlaylistManager.App.Mvvm;
using SynthRidersPlaylistManager.Core.Models;
using System.Collections.ObjectModel;

namespace SynthRidersPlaylistManager.App.ViewModels;

public sealed class SongItemViewModel : ObservableObject
{
    private bool _isFavorite;
    private bool _isBlacklisted;
    private bool _isSourceChecked;
    private bool _isDestinationChecked;

    public SongItemViewModel(Song song)
    {
        Identity = song.Identity;
        Title = song.Title;
        Artist = song.Artist;
        Mapper = song.Mapper;
        Bpm = song.Bpm;
        Duration = song.Duration;
        Difficulty = song.Difficulty;
        _isFavorite = song.IsFavorite;
        _isBlacklisted = song.IsBlacklisted;
        FileName = song.FileName;
        FavoriteReference = song.FavoriteReference;
        PlaylistNames = new ObservableCollection<string>(song.PlaylistNames);
        AddedAt = song.AddedAt;
        Availability = song.Availability;
        IsMetadataComplete = song.IsMetadataComplete;
        CoverState = song.CoverState;
        CoverImagePath = song.CoverImagePath;
        CoverKey = song.CoverKey;
        AudioState = song.AudioState;
        AudioPreviewPath = song.AudioPreviewPath;
    }

    public SongIdentity Identity { get; }
    public string? FileName { get; }
    public string? FavoriteReference { get; }
    public bool IsBlacklisted { get => _isBlacklisted; set { if (SetProperty(ref _isBlacklisted, value)) OnPropertyChanged(nameof(RowOpacity)); } }
    public double RowOpacity => IsBlacklisted ? 0.55 : 1;
    public string Title { get; }
    public string Artist { get; }
    public string Mapper { get; }
    public double? Bpm { get; }
    public TimeSpan? Duration { get; }
    public string Difficulty { get; }
    public ObservableCollection<string> PlaylistNames { get; }
    public DateTimeOffset? AddedAt { get; }
    public SongAvailability Availability { get; }
    public bool IsMetadataComplete { get; }
    public CoverArtState CoverState { get; }
    public string? CoverImagePath { get; }
    public string? CoverKey { get; }
    public AudioPreviewState AudioState { get; }
    public string? AudioPreviewPath { get; }
    public bool HasAudioPreview => AudioState == AudioPreviewState.Available && AudioPreviewPath is not null;
    public bool HasCoverArt => CoverState == CoverArtState.Available && CoverImagePath is not null;
    public string CoverToolTip => CoverState switch
    {
        CoverArtState.Available => "Cover available",
        CoverArtState.InvalidImage => "Cover画像を読み取れないためPlaceholderを表示しています",
        CoverArtState.ParentLocationUnavailable => "Cover保存先を利用できないためPlaceholderを表示しています",
        CoverArtState.ConflictingMapping => "Cover対応が競合しているためPlaceholderを表示しています",
        _ => "Cover未設定 — Placeholder"
    };
    public string SongType => Identity.Kind switch
    {
        SongKind.Custom => "Custom",
        SongKind.OfficialOrDlc => "Official-side / Unknown",
        _ => "Unknown"
    };
    public string PlaylistDisplay => PlaylistNames.Count switch
    {
        0 => "—",
        <= 2 => string.Join(", ", PlaylistNames),
        _ => $"{string.Join(", ", PlaylistNames.Take(2))} +{PlaylistNames.Count - 2}"
    };
    public string PlaylistToolTip => PlaylistNames.Count == 0 ? "所属Playlistなし" : string.Join(Environment.NewLine, PlaylistNames);
    public string DurationDisplay => Duration is null ? "—" : $"{(int)Duration.Value.TotalMinutes}:{Duration.Value.Seconds:00}";
    public string BpmDisplay => Bpm?.ToString("0.#") ?? "—";
    public string MapperDisplay => string.IsNullOrWhiteSpace(Mapper) ? "—" : Mapper;

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteGlyph));
            }
        }
    }

    public string FavoriteGlyph => IsFavorite ? "♥" : "♡";

    public bool IsSourceChecked
    {
        get => _isSourceChecked;
        set => SetProperty(ref _isSourceChecked, value);
    }

    public bool IsDestinationChecked
    {
        get => _isDestinationChecked;
        set => SetProperty(ref _isDestinationChecked, value);
    }

    public bool AddPlaylist(string playlistName)
    {
        if (PlaylistNames.Contains(playlistName, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        PlaylistNames.Add(playlistName);
        OnPropertyChanged(nameof(PlaylistDisplay));
        OnPropertyChanged(nameof(PlaylistToolTip));
        return true;
    }

    public bool RemovePlaylist(string playlistName)
    {
        var existing = PlaylistNames.FirstOrDefault(name => string.Equals(name, playlistName, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return false;
        }

        PlaylistNames.Remove(existing);
        OnPropertyChanged(nameof(PlaylistDisplay));
        OnPropertyChanged(nameof(PlaylistToolTip));
        return true;
    }

    public bool RenamePlaylist(string oldName, string newName)
    {
        var index = PlaylistNames.IndexOf(PlaylistNames.FirstOrDefault(name => string.Equals(name, oldName, StringComparison.OrdinalIgnoreCase)) ?? string.Empty);
        if (index < 0)
        {
            return false;
        }

        PlaylistNames[index] = newName;
        OnPropertyChanged(nameof(PlaylistDisplay));
        OnPropertyChanged(nameof(PlaylistToolTip));
        return true;
    }
}
