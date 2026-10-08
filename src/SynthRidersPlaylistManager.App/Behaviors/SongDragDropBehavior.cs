using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SynthRidersPlaylistManager.App.ViewModels;

namespace SynthRidersPlaylistManager.App.Behaviors;

public static class SongDragDropBehavior
{
    private static readonly DependencyProperty DragStartProperty = DependencyProperty.RegisterAttached(
        "DragStart", typeof(Point), typeof(SongDragDropBehavior), new PropertyMetadata(default(Point)));
    private static readonly DependencyProperty DragPayloadProperty = DependencyProperty.RegisterAttached(
        "DragPayload", typeof(SongDragPayload), typeof(SongDragDropBehavior));
    public static readonly DependencyProperty EnableDragProperty = DependencyProperty.RegisterAttached("EnableDrag", typeof(bool), typeof(SongDragDropBehavior), new PropertyMetadata(false, OnEnableDragChanged));
    public static readonly DependencyProperty EnableDropProperty = DependencyProperty.RegisterAttached("EnableDrop", typeof(bool), typeof(SongDragDropBehavior), new PropertyMetadata(false, OnEnableDropChanged));
    public static readonly DependencyProperty DragOriginProperty = DependencyProperty.RegisterAttached("DragOrigin", typeof(string), typeof(SongDragDropBehavior), new PropertyMetadata("Source"));
    public static readonly DependencyProperty DropCommandProperty = DependencyProperty.RegisterAttached("DropCommand", typeof(ICommand), typeof(SongDragDropBehavior));
    public static readonly DependencyProperty IsDragOverProperty = DependencyProperty.RegisterAttached("IsDragOver", typeof(bool), typeof(SongDragDropBehavior), new PropertyMetadata(false));
    public static readonly DependencyProperty DragFeedbackProperty = DependencyProperty.RegisterAttached("DragFeedback", typeof(string), typeof(SongDragDropBehavior), new PropertyMetadata(""));
    public static void SetEnableDrag(DependencyObject x, bool value) => x.SetValue(EnableDragProperty, value);
    public static bool GetEnableDrag(DependencyObject x) => (bool)x.GetValue(EnableDragProperty);
    public static void SetEnableDrop(DependencyObject x, bool value) => x.SetValue(EnableDropProperty, value);
    public static bool GetEnableDrop(DependencyObject x) => (bool)x.GetValue(EnableDropProperty);
    public static void SetDragOrigin(DependencyObject x, string value) => x.SetValue(DragOriginProperty, value);
    public static string GetDragOrigin(DependencyObject x) => (string)x.GetValue(DragOriginProperty);
    public static void SetDropCommand(DependencyObject x, ICommand value) => x.SetValue(DropCommandProperty, value);
    public static ICommand? GetDropCommand(DependencyObject x) => (ICommand?)x.GetValue(DropCommandProperty);
    public static void SetIsDragOver(DependencyObject x, bool value) => x.SetValue(IsDragOverProperty, value);
    public static bool GetIsDragOver(DependencyObject x) => (bool)x.GetValue(IsDragOverProperty);
    public static void SetDragFeedback(DependencyObject x, string value) => x.SetValue(DragFeedbackProperty, value);
    public static string GetDragFeedback(DependencyObject x) => (string)x.GetValue(DragFeedbackProperty);

    private static void OnEnableDragChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        if ((bool)e.NewValue)
        {
            grid.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(MouseDown), true);
            grid.AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(MouseMove), true);
            DropDiagnostic.Attach(grid);
        }
        else
        {
            grid.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(MouseDown));
            grid.RemoveHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(MouseMove));
            DropDiagnostic.Detach(grid);
            grid.ClearValue(DragPayloadProperty);
        }
    }
    private static void OnEnableDropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return; grid.AllowDrop = (bool)e.NewValue;
        if ((bool)e.NewValue) { grid.DragEnter += DragEnter; grid.DragOver += DragEnter; grid.DragLeave += DragLeave; grid.Drop += Drop; }
        else { grid.DragEnter -= DragEnter; grid.DragOver -= DragEnter; grid.DragLeave -= DragLeave; grid.Drop -= Drop; }
    }
    private static void MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || e.ChangedButton != MouseButton.Left) return;
        if (grid.DataContext is CollectionPaneViewModel pane) pane.Activate();
        grid.SetValue(DragStartProperty, e.GetPosition(grid));
        var origin = e.OriginalSource as DependencyObject;
        grid.SetValue(DragPayloadProperty, IsInteractiveDragBlocker(origin, grid) ? null : CreateDragPayloadForMouseDown(grid, origin));
    }

    internal static SongDragPayload? CreateDragPayloadForMouseDown(DataGrid grid, DependencyObject? origin)
    {
        var item = ResolveDragItem(grid, origin);
        return (grid.DataContext, item) switch
        {
            (CollectionPaneViewModel pane, not null) when pane.CreateDragPayload(item) is { Count: > 0 } payload => payload,
            (MainViewModel vm, not null) when GetDragOrigin(grid) == "Destination" && vm.CreateDestinationDragPayload(item) is { Count: > 0 } payload => payload,
            (MainViewModel vm, not null) when vm.CreateSourceDragPayload(item) is { Count: > 0 } payload => payload,
            _ => null
        };
    }
    private static void MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not DataGrid grid || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(grid);
        var dragStart = (Point)grid.GetValue(DragStartProperty);
        if (!ExceedsDragThreshold(dragStart, current)) return;
        if (grid.GetValue(DragPayloadProperty) is not SongDragPayload payload) return;
        var layer = AdornerLayer.GetAdornerLayer(grid); var adorner = layer is null ? null : new DragCountAdorner(grid, payload.Feedback); if (adorner is not null) layer!.Add(adorner);
        DropDiagnostic.Begin(grid, payload);
        try { DragDrop.DoDragDrop(grid, new DataObject(typeof(SongDragPayload), payload), DragDropEffects.Copy); }
        finally { DropDiagnostic.End(); if (adorner is not null) layer!.Remove(adorner); grid.ClearValue(DragPayloadProperty); }
    }

    internal static bool ExceedsDragThreshold(Point start, Point current) =>
        Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
        Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;

    internal static SongItemViewModel? ResolveDragItem(DataGrid grid, DependencyObject? origin)
    {
        for (var current = origin; current is not null && !ReferenceEquals(current, grid); current = VisualTreeHelper.GetParent(current))
        {
            if (current is DataGridRow row) return row.Item as SongItemViewModel;
        }
        return null;
    }
    internal static bool IsInteractiveDragBlocker(DependencyObject? origin, DataGrid grid)
    {
        for (var current = origin; current is not null && !ReferenceEquals(current, grid); current = VisualTreeHelper.GetParent(current))
        {
            if (current is Button or CheckBox) return true;
        }
        return false;
    }
    private static void DragEnter(object sender, DragEventArgs e)
    {
        if (sender is not DataGrid grid || e.Data.GetData(typeof(SongDragPayload)) is not SongDragPayload payload) { e.Effects = DragDropEffects.None; if (sender is DataGrid invalidGrid) DropDiagnostic.HandlerResult(invalidGrid, e, null, false); return; }
        var command = GetDropCommand(grid); var allowed = command?.CanExecute(payload) == true;
        var targetName = (grid.DataContext as CollectionPaneViewModel)?.CollectionName ?? "Playlist";
        SetIsDragOver(grid, allowed); SetDragFeedback(grid, allowed ? $"＋ {payload.Feedback}を{targetName}へ追加" : "Dropできません");
        e.Effects = allowed ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
        DropDiagnostic.HandlerResult(grid, e, payload, allowed);
    }
    private static void DragLeave(object sender, DragEventArgs e) { if (sender is DataGrid grid) { SetIsDragOver(grid, false); SetDragFeedback(grid, ""); } }
    private static void Drop(object sender, DragEventArgs e)
    {
        if (sender is not DataGrid grid) return; SetIsDragOver(grid, false);
        var payload = e.Data.GetData(typeof(SongDragPayload)) as SongDragPayload; var command = GetDropCommand(grid);
        if (payload is not null && command?.CanExecute(payload) == true) { DropDiagnostic.HandlerResult(grid, e, payload, true); command.Execute(payload); e.Effects = DragDropEffects.Copy; } else { e.Effects = DragDropEffects.None; DropDiagnostic.HandlerResult(grid, e, payload, false); }
        SetDragFeedback(grid, ""); e.Handled = true;
    }

    private sealed class DragCountAdorner : Adorner
    {
        private readonly string _text;
        public DragCountAdorner(UIElement adorned, string text) : base(adorned) { _text = text; IsHitTestVisible = false; }
        protected override void OnRender(DrawingContext dc)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var formatted = new FormattedText(_text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 15, Brushes.White, dpi.PixelsPerDip);
            var rect = new Rect(18, 18, formatted.Width + 28, formatted.Height + 18); dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(235, 92, 55, 130)), new Pen(Brushes.MediumPurple, 1), rect, 7, 7); dc.DrawText(formatted, new Point(32, 27));
        }
    }
}
