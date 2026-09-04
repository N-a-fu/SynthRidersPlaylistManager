using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SynthRidersPlaylistManager.App.Converters;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.App.Views;

namespace SynthRidersPlaylistManager.Tests;

public sealed class PaneRowBackgroundConverterTests
{
    [Theory]
    [InlineData(true, false, false, PaneSide.A)]
    [InlineData(false, true, false, PaneSide.A)]
    [InlineData(false, false, true, PaneSide.B)]
    [InlineData(true, true, true, PaneSide.B)]
    public void SelectedOrPaneCheckedRowsUseTheSameBlueHighlight(
        bool selected, bool sourceChecked, bool destinationChecked, PaneSide side)
    {
        RunSta(() =>
        {
            var converter = new PaneRowBackgroundConverter();
            var brush = Assert.IsType<SolidColorBrush>(converter.Convert(
                [selected, sourceChecked, destinationChecked, side, true, false, 0],
                typeof(Brush), null, CultureInfo.InvariantCulture));

            Assert.Equal(Color.FromRgb(0x28, 0x5A, 0x86), brush.Color);
        });
    }

    [Fact]
    public void OtherPaneCheckDoesNotHighlightTheRow()
    {
        RunSta(() =>
        {
            var converter = new PaneRowBackgroundConverter();
            var paneABrush = Assert.IsType<SolidColorBrush>(converter.Convert(
                [false, false, true, PaneSide.A, true, false, 0], typeof(Brush), null, CultureInfo.InvariantCulture));
            var paneBBrush = Assert.IsType<SolidColorBrush>(converter.Convert(
                [false, true, false, PaneSide.B, true, false, 0], typeof(Brush), null, CultureInfo.InvariantCulture));

            Assert.Equal(Color.FromRgb(0x1E, 0x1D, 0x23), paneABrush.Color);
            Assert.Equal(Color.FromRgb(0x1E, 0x1D, 0x23), paneBBrush.Color);
        });
    }

    [Fact]
    public void HoverIsDarkGrayAndDoesNotOverrideSelection()
    {
        RunSta(() =>
        {
            var converter = new PaneRowBackgroundConverter();
            var hover = Assert.IsType<SolidColorBrush>(converter.Convert(
                [false, false, false, PaneSide.A, true, true, 0], typeof(Brush), null, CultureInfo.InvariantCulture));
            var selectedHover = Assert.IsType<SolidColorBrush>(converter.Convert(
                [true, false, false, PaneSide.A, true, true, 0], typeof(Brush), null, CultureInfo.InvariantCulture));

            Assert.Equal(Color.FromRgb(0x30, 0x2C, 0x39), hover.Color);
            Assert.Equal(Color.FromRgb(0x28, 0x5A, 0x86), selectedHover.Color);
        });
    }

    [Fact]
    public void InactivePaneKeepsSelectionStateWithoutStrongHighlight()
    {
        RunSta(() =>
        {
            var converter = new PaneRowBackgroundConverter();
            var brush = Assert.IsType<SolidColorBrush>(converter.Convert(
                [true, true, false, PaneSide.A, false, false, 0],
                typeof(Brush), null, CultureInfo.InvariantCulture));

            Assert.Equal(Color.FromRgb(0x1E, 0x1D, 0x23), brush.Color);
        });
    }

    [Fact]
    public void BrowserUsesExtendedFullRowSelectionAndTransparentCells()
    {
        RunSta(() =>
        {
            var application = Application.Current ?? new Application();
            if (!application.Resources.MergedDictionaries.Any(x => x.Source?.OriginalString.Contains("Theme.xaml", StringComparison.Ordinal) == true))
            {
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/SynthRidersPlaylistManager.App;component/Resources/Theme.xaml")
                });
            }
            var view = new CollectionBrowserView { DataContext = new MainViewModel().PaneA };
            var grid = Assert.IsType<DataGrid>(view.FindName("SongsGrid"));

            Assert.Equal(DataGridSelectionMode.Extended, grid.SelectionMode);
            Assert.Equal(DataGridSelectionUnit.FullRow, grid.SelectionUnit);

            var cell = new DataGridCell { Style = grid.FindResource(typeof(DataGridCell)) as System.Windows.Style };
            Assert.Equal(Brushes.Transparent, cell.Background);
            Assert.Null(cell.FocusVisualStyle);
        });
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception exception) { failure = exception; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
