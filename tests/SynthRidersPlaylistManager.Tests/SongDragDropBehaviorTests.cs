using System.Windows.Controls;
using System.Windows;
using System.Reflection;
using SynthRidersPlaylistManager.Core.Models;
using System.Runtime.ExceptionServices;
using SynthRidersPlaylistManager.App.Behaviors;
using SynthRidersPlaylistManager.App.ViewModels;

namespace SynthRidersPlaylistManager.Tests;

public sealed class SongDragDropBehaviorTests
{
    [Fact]
    public void DragCandidateComesFromMouseDownRowEvenWhenGridHasNoSelection()
    {
        RunSta(() =>
        {
        var vm = ProductionViewModelFixture.Create();
            var expected = vm.PaneB.VisibleSongs.Cast<SongItemViewModel>().First();
            var grid = new DataGrid { SelectedItem = null };
            var row = new DataGridRow { Item = expected };

            var actual = SongDragDropBehavior.ResolveDragItem(grid, row);

            Assert.Same(expected, actual);
            Assert.Null(grid.SelectedItem);
        });
    }

    [Fact]
    public void BothPaneDropTargetsEnableAllowDropAndKeepOwnCommands()
    {
        RunSta(() =>
        {
        var vm = ProductionViewModelFixture.Create();
            var paneAGrid = new DataGrid { DataContext = vm.PaneA };
            var paneBGrid = new DataGrid { DataContext = vm.PaneB };

        SongDragDropBehavior.SetEnableDrop(paneAGrid, true);
        SongDragDropBehavior.SetDropCommand(paneAGrid, vm.PaneA.DropCommand);
        SongDragDropBehavior.SetEnableDrop(paneBGrid, true);
        SongDragDropBehavior.SetDropCommand(paneBGrid, vm.PaneB.DropCommand);

        Assert.True(paneAGrid.AllowDrop);
        Assert.True(paneBGrid.AllowDrop);
        Assert.Same(vm.PaneA.DropCommand, SongDragDropBehavior.GetDropCommand(paneAGrid));
            Assert.Same(vm.PaneB.DropCommand, SongDragDropBehavior.GetDropCommand(paneBGrid));
        });
    }

    [Fact]
    public void MouseDownPayloadUsesCheckedSongsFromActualPaneInBothDirections()
    {
        RunSta(() =>
        {
        var vm = ProductionViewModelFixture.Create();
            vm.Songs[2].IsSourceChecked = true;
            vm.Songs[3].IsSourceChecked = true;
            vm.Songs[4].IsDestinationChecked = true;
            vm.Songs[5].IsDestinationChecked = true;
            var gridA = new DataGrid { DataContext = vm.PaneA };
            var gridB = new DataGrid { DataContext = vm.PaneB };

            var payloadA = SongDragDropBehavior.CreateDragPayloadForMouseDown(gridA, new DataGridRow { Item = vm.Songs[2] });
            var payloadB = SongDragDropBehavior.CreateDragPayloadForMouseDown(gridB, new DataGridRow { Item = vm.Songs[4] });

            Assert.NotNull(payloadA); Assert.Equal("A", payloadA.Origin); Assert.Equal(2, payloadA.Count);
            Assert.NotNull(payloadB); Assert.Equal("B", payloadB.Origin); Assert.Equal(2, payloadB.Count);
        });
    }

    [Fact]
    public void FavoriteAndCheckboxControlsDoNotStartSongDrag()
    {
        RunSta(() =>
        {
            var grid = new DataGrid();

            Assert.True(SongDragDropBehavior.IsInteractiveDragBlocker(new Button(), grid));
            Assert.True(SongDragDropBehavior.IsInteractiveDragBlocker(new CheckBox(), grid));
            Assert.False(SongDragDropBehavior.IsInteractiveDragBlocker(new DataGridRow(), grid));
        });
    }

    [Fact]
    public void BlacklistedRowDoesNotCreateMouseDownDragPayload()
    {
        RunSta(() =>
        {
            using var vm = ProductionViewModelFixture.Create();
            var song = vm.Songs.First();
            song.IsBlacklisted = true;
            var grid = new DataGrid { DataContext = vm.PaneA };

            var payload = SongDragDropBehavior.CreateDragPayloadForMouseDown(grid, new DataGridRow { Item = song });

            Assert.Null(payload);
        });
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void ActualMouseDownPayloadAndDropCommandAddSymmetrically(bool fromA, bool multi)
    {
        RunSta(() =>
        {
        using var vm = ProductionViewModelFixture.Create();
            var source = fromA ? vm.PaneA : vm.PaneB;
            var target = fromA ? vm.PaneB : vm.PaneA;
            source.SelectedCollection = vm.PlaylistNavigation.First(item => item.PlaylistName == "Test Playlist A");
            target.SelectedCollection = vm.PlaylistNavigation.First(item => item.PlaylistName == "Empty Playlist");
            var songs = vm.Songs.Take(3).ToArray();
            foreach (var song in songs) song.AddPlaylist("Test Playlist A");
            source.Refresh(); target.Refresh();
            // Checked songs in the opposite pane must not influence the payload.
            target.ToggleCheckedCommand.Execute(songs[2]);
            target.SelectedSong = songs[2];
            source.ToggleCheckedCommand.Execute(songs[1]);
            if (multi) source.ToggleCheckedCommand.Execute(songs[0]);
            var sourceGrid = new DataGrid { DataContext = source };
            var targetGrid = new DataGrid { DataContext = target };
            SongDragDropBehavior.SetEnableDrop(targetGrid, true);
            SongDragDropBehavior.SetDropCommand(targetGrid, target.DropCommand);
            var payload = SongDragDropBehavior.CreateDragPayloadForMouseDown(sourceGrid, new DataGridRow { Item = songs[0] });
            Assert.NotNull(payload);
            Assert.Equal(multi ? 2 : 1, payload.Count);
            Assert.Equal("Test Playlist A", payload.SourcePlaylist);
            Assert.True(targetGrid.AllowDrop);
            Assert.True(target.DropCommand.CanExecute(payload));
            var data = new DataObject(typeof(SongDragPayload), payload);
            // WPF's DragEventArgs constructor is internal; deliver the event through
            // the actual registered DataGrid handler, without starting an OS drag loop.
            System.Windows.DragEventArgs Event(RoutedEvent routedEvent)
            {
                var args = (System.Windows.DragEventArgs)Activator.CreateInstance(typeof(System.Windows.DragEventArgs),
                    BindingFlags.Instance | BindingFlags.NonPublic, null,
                    [data, System.Windows.DragDropKeyStates.LeftMouseButton, DragDropEffects.Copy, targetGrid, new Point()], null)!;
                args.RoutedEvent = routedEvent;
                return args;
            }
            var over = Event(DragDrop.DragOverEvent);
            targetGrid.RaiseEvent(over);
            Assert.Equal(DragDropEffects.Copy, over.Effects);
            var drop = Event(DragDrop.DropEvent);
            targetGrid.RaiseEvent(drop);
            Assert.True(drop.Handled);
            Assert.Equal(DragDropEffects.Copy, drop.Effects);
            Assert.Contains("Empty Playlist", songs[0].PlaylistNames);
            Assert.Equal(multi, songs[1].PlaylistNames.Contains("Empty Playlist"));
            Assert.DoesNotContain("Empty Playlist", songs[2].PlaylistNames);
            Assert.All(songs, s => Assert.Contains("Test Playlist A", s.PlaylistNames));
            Assert.Equal(multi ? 2 : 1, target.VisibleCount);
            Assert.Equal(3, source.VisibleCount);
            Assert.True(target.IsSongChecked(songs[2]));
            Assert.False(target.IsSongChecked(songs[0]));
            Assert.Same(songs[2], target.SelectedSong);
            target.DropCommand.Execute(payload);
            Assert.Single(songs[0].PlaylistNames, n => n == "Empty Playlist");
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DropRejectsSamePlaylistNonPlaylistWrongOriginAndStaleSource(bool fromA)
    {
        RunSta(() =>
        {
        using var vm = ProductionViewModelFixture.Create();
            var source = fromA ? vm.PaneA : vm.PaneB;
            var target = fromA ? vm.PaneB : vm.PaneA;
            source.SelectedCollection = vm.PlaylistNavigation.First(item => item.PlaylistName == "Test Playlist A");
            target.SelectedCollection = source.SelectedCollection;
            var song = vm.Songs.First(item => item.PlaylistNames.Contains("Test Playlist A"));
            var payload = source.CreateDragPayload(song);
            Assert.False(target.DropCommand.CanExecute(payload));
            target.SelectedCollection = new("All", NavigationFilter.AllSongs);
            Assert.False(target.DropCommand.CanExecute(payload));
            target.SelectedCollection = vm.PlaylistNavigation.First(item => item.PlaylistName == "Test Playlist B");
            source.SelectedCollection = new("All", NavigationFilter.AllSongs);
            Assert.True(target.DropCommand.CanExecute(source.CreateDragPayload(song)));
            source.SelectedCollection = vm.PlaylistNavigation.First(item => item.PlaylistName == "Test Playlist A");
            Assert.False(target.DropCommand.CanExecute(payload with { Origin = target.Side.ToString() }));
            Assert.False(target.DropCommand.CanExecute(payload with { Origin = "Source" }));
            source.SelectedCollection = vm.PlaylistNavigation.First(item => item.PlaylistName == "Other Playlist");
            Assert.False(target.DropCommand.CanExecute(payload));
            target.DropCommand.Execute(payload);
            Assert.DoesNotContain("Test Playlist B", song.PlaylistNames);
            source.SelectedCollection = vm.PlaylistNavigation.First(item => item.PlaylistName == "Test Playlist A");
            vm.GameState = GameAccessState.RunningReadOnly;
            Assert.False(target.DropCommand.CanExecute(payload));
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
