using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SynthRidersPlaylistManager.App.Converters;

public sealed class SelectedCollectionBrushConverter : IMultiValueConverter
{
    private static readonly Brush Selected = new SolidColorBrush(Color.FromRgb(78, 51, 100));
    private static readonly Brush Transparent = Brushes.Transparent;

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length >= 2 && ReferenceEquals(values[0], values[1]) ? Selected : Transparent;

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
