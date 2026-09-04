using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SynthRidersPlaylistManager.App.Controls;

public sealed class CoverArtView : Grid
{
    private static readonly ConcurrentDictionary<CacheKey, Lazy<Task<ImageSource?>>> Cache = new();
    private readonly Image _image;
    private string? _attemptedPath;
    private int _attemptedWidth;

    public CoverArtView()
    {
        ClipToBounds = true;
        Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(73, 48, 93)),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = "♫", Foreground = new SolidColorBrush(Color.FromRgb(180, 108, 255)), FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        });
        _image = new Image { Stretch = Stretch.Uniform, SnapsToDevicePixels = true };
        Children.Add(_image);
        Loaded += (_, _) => _ = RefreshAsync();
    }

    public static readonly DependencyProperty CoverPathProperty = DependencyProperty.Register(
        nameof(CoverPath), typeof(string), typeof(CoverArtView), new PropertyMetadata(null, OnCoverChanged));
    public static readonly DependencyProperty DecodePixelWidthProperty = DependencyProperty.Register(
        nameof(DecodePixelWidth), typeof(int), typeof(CoverArtView), new PropertyMetadata(48, OnCoverChanged));

    public string? CoverPath { get => (string?)GetValue(CoverPathProperty); set => SetValue(CoverPathProperty, value); }
    public int DecodePixelWidth { get => (int)GetValue(DecodePixelWidthProperty); set => SetValue(DecodePixelWidthProperty, value); }

    private static void OnCoverChanged(DependencyObject target, DependencyPropertyChangedEventArgs args) =>
        _ = ((CoverArtView)target).RefreshAsync();

    private async Task RefreshAsync()
    {
        var requestedPath = CoverPath;
        var requestedWidth = Math.Clamp(DecodePixelWidth, 16, 256);
        if (!IsLoaded) return;
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            _attemptedPath = null;
            _attemptedWidth = 0;
            _image.Source = null;
            return;
        }
        if (string.Equals(_attemptedPath, requestedPath, StringComparison.OrdinalIgnoreCase) && _attemptedWidth == requestedWidth) return;

        _attemptedPath = requestedPath;
        _attemptedWidth = requestedWidth;
        _image.Source = null;

        var key = new CacheKey(requestedPath, requestedWidth);
        var image = await Cache.GetOrAdd(key, static item => new(() => DecodeAsync(item))).Value;
        if (image is null)
        {
            Cache.TryRemove(key, out _);
            _attemptedPath = null;
            _attemptedWidth = 0;
            return;
        }
        if (IsLoaded && string.Equals(CoverPath, requestedPath, StringComparison.OrdinalIgnoreCase) && DecodePixelWidth == requestedWidth)
            _image.Source = image;
    }

    private static Task<ImageSource?> DecodeAsync(CacheKey key) => Task.Run<ImageSource?>(() =>
    {
        try
        {
            using var stream = new FileStream(key.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = key.Width;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    });

    private readonly record struct CacheKey(string Path, int Width);
}
