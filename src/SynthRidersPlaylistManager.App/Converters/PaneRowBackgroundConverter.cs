using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SynthRidersPlaylistManager.App.ViewModels;

namespace SynthRidersPlaylistManager.App.Converters;

public sealed class PaneRowBackgroundConverter : IMultiValueConverter
{
    private static readonly Brush HighlightBrush = CreateBrush("#285A86");
    private static readonly Brush HoverBrush = CreateBrush("#302C39");
    private static readonly Brush RowBrush = CreateBrush("#1E1D23");
    private static readonly Brush AlternateRowBrush = CreateBrush("#222128");

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var rowSelected = values.Length > 0 && values[0] is true;
        var sourceChecked = values.Length > 1 && values[1] is true;
        var destinationChecked = values.Length > 2 && values[2] is true;
        var side = values.Length > 3 && values[3] is PaneSide paneSide ? paneSide : PaneSide.A;
        var isActive = values.Length > 4 && values[4] is true;
        var mouseOver = values.Length > 5 && values[5] is true;
        var alternationIndex = values.Length > 6 && values[6] is int index ? index : 0;
        var paneChecked = side == PaneSide.A ? sourceChecked : destinationChecked;

        if (isActive && (rowSelected || paneChecked)) return HighlightBrush;
        if (mouseOver) return HoverBrush;
        return alternationIndex % 2 == 0 ? RowBrush : AlternateRowBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
