using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using SynthRidersPlaylistManager.App.ViewModels;

namespace SynthRidersPlaylistManager.App.Behaviors;

// Temporary, observational diagnostics only. Never participates in drop decisions.
internal static class DropDiagnostic
{
    private static readonly List<WeakReference<DataGrid>> Grids = [];
    private static readonly List<string> Records = [];
    private static readonly HashSet<string> Seen = [];
    private static bool _active;
    private static int _overObserved, _dropObserved, _overHandler, _dropHandler;

    internal static void Attach(DataGrid grid)
    {
        Grids.Add(new(grid));
        // Observe even events handled by a cell/row. Do not change Handled or Effects.
        grid.AddHandler(DragDrop.DragOverEvent, new DragEventHandler(Observe), true);
        grid.AddHandler(DragDrop.DropEvent, new DragEventHandler(Observe), true);
    }

    internal static void Detach(DataGrid grid)
    {
        grid.RemoveHandler(DragDrop.DragOverEvent, new DragEventHandler(Observe));
        grid.RemoveHandler(DragDrop.DropEvent, new DragEventHandler(Observe));
        Grids.RemoveAll(r => !r.TryGetTarget(out var g) || ReferenceEquals(g, grid));
    }

    internal static void Begin(DataGrid source, SongDragPayload payload)
    {
        try
        {
            Records.Clear(); Seen.Clear();
            _overObserved = _dropObserved = _overHandler = _dropHandler = 0;
            _active = true;
            Records.Add(JsonSerializer.Serialize(new { Event = "DragBegin", Time = DateTimeOffset.Now, ProcessId = Environment.ProcessId }));
            var owner = (source.DataContext as CollectionPaneViewModel)?.Owner;
            foreach (var reference in Grids)
                if (reference.TryGetTarget(out var grid) && grid.DataContext is CollectionPaneViewModel pane && ReferenceEquals(pane.Owner, owner))
                    Record("GridAtDragBegin", grid, payload, null, null, null);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
    }

    private static void Observe(object sender, DragEventArgs e)
    {
        try
        {
            if (!_active || sender is not DataGrid grid) return;
            if (e.RoutedEvent == DragDrop.DropEvent) _dropObserved++; else _overObserved++;
            Record(e.RoutedEvent.Name + "Observed", grid, e.Data.GetData(typeof(SongDragPayload)) as SongDragPayload, e.Effects, e.Handled, null);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
    }

    internal static void HandlerResult(DataGrid grid, DragEventArgs e, SongDragPayload? payload, bool accepted)
    {
        try
        {
            if (!_active) return;
            if (e.RoutedEvent == DragDrop.DropEvent) _dropHandler++;
            if (e.RoutedEvent == DragDrop.DragOverEvent) _overHandler++;
            Record(e.RoutedEvent.Name + "HandlerResult", grid, payload, e.Effects, e.Handled, accepted);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
    }

    private static void Record(string stage, DataGrid grid, SongDragPayload? payload, DragDropEffects? effects, bool? handled, bool? accepted)
    {
        var destination = grid.DataContext as CollectionPaneViewModel;
        var owner = destination?.Owner;
        var source = payload?.Origin == "A" ? owner?.PaneA : payload?.Origin == "B" ? owner?.PaneB : null;
        var expectedSource = destination == owner?.PaneA ? owner?.PaneB : owner?.PaneA;
        var reasons = new List<string>();
        if (destination is null) reasons.Add("DestinationPaneMissing");
        if (owner is not null)
        {
            if (destination != owner.PaneA && destination != owner.PaneB) reasons.Add("DestinationPaneNotOwned");
            if (!owner.IsDragDropEnabled) reasons.Add("IsDragDropEnabledFalse");
            if (destination?.IsPlaylist != true) reasons.Add("DestinationNotPlaylist");
            if (string.IsNullOrWhiteSpace(destination?.SelectedCollection?.PlaylistName)) reasons.Add("DestinationPlaylistIdentifierMissing");
            if (expectedSource?.IsPlaylist == true && string.Equals(expectedSource.SelectedCollection?.PlaylistName, destination?.SelectedCollection?.PlaylistName, StringComparison.OrdinalIgnoreCase)) reasons.Add("SamePlaylist");
        }
        if (!grid.AllowDrop) reasons.Add("AllowDropFalse");
        if (SongDragDropBehavior.GetDropCommand(grid) is null) reasons.Add("DropCommandMissing");
        if (payload is null) reasons.Add("PayloadMissingOrWrongType");
        else
        {
            if (payload.Count == 0) reasons.Add("PayloadEmpty");
            if (payload.Origin != expectedSource?.Side.ToString()) reasons.Add("SourcePaneNotOpposite");
            if (expectedSource?.IsPlaylist == true && string.IsNullOrWhiteSpace(payload.SourcePlaylist)) reasons.Add("PayloadSourcePlaylistMissing");
            if (!string.Equals(payload.SourcePlaylist, expectedSource?.IsPlaylist == true ? expectedSource.SelectedCollection?.PlaylistName : null, StringComparison.OrdinalIgnoreCase)) reasons.Add("SourcePlaylistChanged");
            if (owner is not null && !payload.Songs.All(owner.Songs.Contains)) reasons.Add("PayloadSongNotInLibrary");
        }
        if (accepted == false && reasons.Count == 0) reasons.Add("CommandCanExecuteFalse_Unclassified");
        var line = JsonSerializer.Serialize(new
        {
            Event = stage, SourcePane = payload?.Origin, DestinationPane = destination?.Side.ToString(),
            SourceIsPlaylist = source?.IsPlaylist, DestinationIsPlaylist = destination?.IsPlaylist,
            SourcePlaylistIdentifier = source?.SelectedCollection?.PlaylistName,
            DestinationPlaylistIdentifier = destination?.SelectedCollection?.PlaylistName,
            PayloadSourcePlaylistIdentifier = payload?.SourcePlaylist,
            IsDragDropEnabled = owner?.IsDragDropEnabled, grid.AllowDrop,
            GameRunningState = owner?.GameState.ToString(), PayloadSongCount = payload?.Count,
            Effects = effects?.ToString(), Handled = handled, HandlerAccepted = accepted,
            // Snapshot of existing guards, not a replacement for command.CanExecute.
            FailedConditions = reasons
        });
        // Avoid mouse-move log floods; counts remain exact in DragEnd.
        if (Seen.Add(line) && Records.Count < 64) Records.Add(line);
    }

    internal static void End()
    {
        try
        {
            if (!_active) return;
            Records.Add(JsonSerializer.Serialize(new { Event = "DragEnd", Time = DateTimeOffset.Now,
                DragOverObserved = _overObserved, DropObserved = _dropObserved,
                DragOverHandlerCalls = _overHandler, DropHandlerCalls = _dropHandler }));
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynthRidersPlaylistManager", "Logs");
            Directory.CreateDirectory(directory);
            File.AppendAllLines(Path.Combine(directory, "drag-drop-diagnostic.log"), Records);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        finally { _active = false; Records.Clear(); Seen.Clear(); }
    }
}
