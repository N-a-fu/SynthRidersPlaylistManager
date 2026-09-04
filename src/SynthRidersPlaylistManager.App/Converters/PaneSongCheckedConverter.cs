using System.Globalization;
using System.Windows.Data;
using SynthRidersPlaylistManager.App.ViewModels;

namespace SynthRidersPlaylistManager.App.Converters;

public sealed class PaneSongCheckedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length >= 3 && values[0] is bool sourceChecked && values[1] is bool destinationChecked &&
        values[2] is CollectionPaneViewModel pane && (pane.Side == PaneSide.A ? sourceChecked : destinationChecked);

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
