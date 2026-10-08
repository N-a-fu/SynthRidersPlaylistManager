using System.ComponentModel;
using SynthRidersPlaylistManager.App.ViewModels;

namespace SynthRidersPlaylistManager.Tests;

public sealed class CollectionPaneSortingTests
{
    [Fact]
    public void SortTogglesThreeColumnsIndependentlyAndPreservesUnderlyingOrderAcrossFilterChanges()
    {
        using var vm = ProductionViewModelFixture.Create();
        var paneA = vm.PaneA;
        var paneB = vm.PaneB;
        var original = vm.Songs.ToArray();

        VerifyCycle(paneA, nameof(SongItemViewModel.Title), s => s.Title);
        VerifyCycle(paneA, nameof(SongItemViewModel.Artist), s => s.Artist);
        VerifyCycle(paneA, nameof(SongItemViewModel.MapperDisplay), s => s.MapperDisplay);

        paneA.CycleSort(nameof(SongItemViewModel.Artist));
        paneB.CycleSort(nameof(SongItemViewModel.MapperDisplay));
        paneB.CycleSort(nameof(SongItemViewModel.MapperDisplay));
        Assert.Equal(SongSortColumn.Artist, paneA.SortColumn);
        Assert.Equal(ListSortDirection.Ascending, paneA.SortDirection);
        Assert.Equal(SongSortColumn.Mapper, paneB.SortColumn);
        Assert.Equal(ListSortDirection.Descending, paneB.SortDirection);

        paneA.SearchText = "a";
        Assert.Equal(SongSortColumn.Artist, paneA.SortColumn);
        AssertOrdered(paneA.VisibleSongs.Cast<SongItemViewModel>().Select(s => s.Artist), false);
        paneA.SelectedCollection = new("Favorites", NavigationFilter.Favorites);
        Assert.Equal(SongSortColumn.Artist, paneA.SortColumn);
        AssertOrdered(paneA.VisibleSongs.Cast<SongItemViewModel>().Select(s => s.Artist), false);

        var playlistName = vm.PlaylistNavigation.First().PlaylistName!;
        paneA.SelectedCollection = new("Playlist", NavigationFilter.Playlist, playlistName);
        var membershipOrder = vm.Songs.Where(s => s.PlaylistNames.Contains(playlistName)).ToArray();
        paneA.CycleSort(nameof(SongItemViewModel.Title));
        Assert.Equal(membershipOrder, vm.Songs.Where(s => s.PlaylistNames.Contains(playlistName)));
        Assert.Equal(original, vm.Songs);
    }

    private static void VerifyCycle(CollectionPaneViewModel pane, string property, Func<SongItemViewModel, string> selector)
    {
        pane.CycleSort(property);
        Assert.Equal(ListSortDirection.Ascending, pane.SortDirection);
        AssertOrdered(pane.VisibleSongs.Cast<SongItemViewModel>().Select(selector), false);
        pane.CycleSort(property);
        Assert.Equal(ListSortDirection.Descending, pane.SortDirection);
        AssertOrdered(pane.VisibleSongs.Cast<SongItemViewModel>().Select(selector), true);
        pane.CycleSort(property);
        Assert.NotNull(pane.SortColumn);
        Assert.Equal(ListSortDirection.Ascending, pane.SortDirection);
        AssertOrdered(pane.VisibleSongs.Cast<SongItemViewModel>().Select(selector), false);
        pane.CycleSort(property);
        Assert.NotNull(pane.SortColumn);
        Assert.Equal(ListSortDirection.Descending, pane.SortDirection);
        AssertOrdered(pane.VisibleSongs.Cast<SongItemViewModel>().Select(selector), true);
    }

    private static void AssertOrdered(IEnumerable<string> values, bool descending)
    {
        var actual = values.ToArray();
        var expected = descending
            ? actual.OrderByDescending(x => x, StringComparer.CurrentCulture).ToArray()
            : actual.OrderBy(x => x, StringComparer.CurrentCulture).ToArray();
        Assert.Equal(expected, actual);
    }
}
