using System.IO;
using System.Text.Json;
using System.Windows;

namespace SynthRidersPlaylistManager.App.Services;

public sealed record UiSettings(string Language = "ja-JP");

public sealed class UiSettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string Path { get; } = path;
    public UiSettings Load()
    {
        try { return File.Exists(Path) ? JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(Path)) ?? new() : new(); }
        catch { return new(); }
    }
    public void Save(UiSettings settings)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(Path, JsonSerializer.Serialize(settings, Options));
    }
    public static UiSettingsStore CreateDefault() => new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynthRidersPlaylistManager", "ui-settings.json"));
}

public static class LocalizationService
{
    public static string CurrentLanguage { get; private set; } = "ja-JP";
    public static void Initialize() => Apply(UiSettingsStore.CreateDefault().Load().Language, false);
    public static void Toggle() => Apply(CurrentLanguage == "ja-JP" ? "en-US" : "ja-JP", true);
    public static void Apply(string language, bool save)
    {
        var normalized = string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "ja-JP";
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(d => d.Source?.OriginalString.Contains("Strings.", StringComparison.OrdinalIgnoreCase) == true);
        var replacement = new ResourceDictionary { Source = new Uri($"/SynthRidersPlaylistManager.App;component/Resources/Strings.{normalized}.xaml", UriKind.Relative) };
        if (existing is null) dictionaries.Add(replacement); else dictionaries[dictionaries.IndexOf(existing)] = replacement;
        CurrentLanguage = normalized;
        if (save) UiSettingsStore.CreateDefault().Save(new(normalized));
    }
}
