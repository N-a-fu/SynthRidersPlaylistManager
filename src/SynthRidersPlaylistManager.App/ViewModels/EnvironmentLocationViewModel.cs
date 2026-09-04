using SynthRidersPlaylistManager.App.Mvvm;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.App.ViewModels;

public sealed class EnvironmentLocationViewModel(DataLocation location) : ObservableObject
{
    private DataLocation _location = location;
    public DataLocationKind Kind => _location.Kind;
    public string DisplayName => DisplayNameFor(Kind);
    public string PathDisplay => _location.ResolvedPath ?? "未解決";
    public string StatusDisplay => _location.Status switch
    {
        DataLocationStatus.Available => "利用可能",
        DataLocationStatus.Unavailable => "アクセス不可",
        DataLocationStatus.Missing => "見つかりません",
        DataLocationStatus.Invalid => "無効",
        DataLocationStatus.NotConfigured => "未設定",
        _ => "確認不十分"
    };
    public string SourceDisplay => _location.Source switch
    {
        DataLocationSource.AutoDetected => "自動検出",
        DataLocationSource.UserOverride => "手動指定",
        DataLocationSource.Derived => "検出Rootから個別検証",
        _ => "未解決"
    };
    public string ValidationMessage => _location.ValidationMessage;
    public string LastValidatedDisplay => _location.LastValidated.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public bool IsWarning => _location.Status is not DataLocationStatus.Available;

    public void Update(DataLocation location)
    {
        _location = location;
        OnPropertyChanged(string.Empty);
    }

    public static string DisplayNameFor(DataLocationKind kind) => kind switch
    {
        DataLocationKind.GameRoot => "Synth Riders / Game Root",
        DataLocationKind.Playlists => "Playlists",
        DataLocationKind.Favorites => "Favorites",
        DataLocationKind.CustomSongs => "Custom Songs",
        DataLocationKind.SynthDatabase => "SynthDB",
        DataLocationKind.ImagesCache => "ImagesCache",
        DataLocationKind.TempAudio => "tempExt（存在確認のみ）",
        _ => kind.ToString()
    };
}
