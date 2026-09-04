using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using SynthRidersPlaylistManager.App.Services;
using SynthRidersPlaylistManager.App.ViewModels;

namespace SynthRidersPlaylistManager.Tests;

public sealed class LocalizationSwitchTests
{
    [Fact]
    public void SwitchesBothPanesImmediatelyPreservesStateAndPersistsLanguage()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new SynthRidersPlaylistManager.App.App();
                app.InitializeComponent();
                LocalizationService.Apply("ja-JP", false);
                using var vm = new MainViewModel();
                var playlistA = vm.PlaylistNavigation.First();
                var playlistB = vm.PlaylistNavigation.Last();
                vm.PaneA.SelectedCollection = playlistA;
                vm.PaneB.SelectedCollection = playlistB;
                vm.PaneA.SearchText = "a";
                vm.PaneA.CycleSort(nameof(SongItemViewModel.Artist));
                var song = vm.Songs.First();
                vm.PaneA.ToggleCheckedCommand.Execute(song);
                vm.SelectedSong = song;
                var title = song.Title; var artist = song.Artist; var mapper = song.Mapper;

                vm.ToggleLanguageCommand.Execute(null);
                Assert.Equal("en-US", LocalizationService.CurrentLanguage);
                Assert.Equal("All Favorites", vm.FavoritesNavigation[0].Label);
                Assert.Equal("All Songs", vm.SmartNavigation[0].Label);
                Assert.Equal("Blacklist", vm.SmartNavigation.Last().Label);
                Assert.Equal("en-US", UiSettingsStore.CreateDefault().Load().Language);
                LocalizationService.Apply("ja-JP", false);
                LocalizationService.Initialize();
                Assert.Equal("en-US", LocalizationService.CurrentLanguage); // simulated restart load
                Assert.Same(playlistA, vm.PaneA.SelectedCollection);
                Assert.Same(playlistB, vm.PaneB.SelectedCollection);
                Assert.Equal("a", vm.PaneA.SearchText);
                Assert.True(vm.PaneA.IsSongChecked(song));
                Assert.Same(song, vm.SelectedSong);
                Assert.Equal(ListSortDirection.Ascending, vm.PaneA.SortDirection);
                Assert.Equal((title, artist, mapper), (song.Title, song.Artist, song.Mapper));

                vm.ToggleLanguageCommand.Execute(null);
                Assert.Equal("ja-JP", LocalizationService.CurrentLanguage);
                Assert.Equal("すべてのお気に入り", vm.FavoritesNavigation[0].Label);
                Assert.Equal("すべての曲", vm.SmartNavigation[0].Label);
                Assert.Equal("ブラックリスト", vm.SmartNavigation.Last().Label);
                Assert.Equal("ja-JP", UiSettingsStore.CreateDefault().Load().Language);

                // Existing behavior remains enum/identity-driven, never label-driven.
                Assert.True(vm.PaneA.IsPlaylist);
                Assert.NotNull(vm.PaneB.DropCommand);
                Assert.NotNull(vm.PaneA.RemoveFromCurrentPlaylistCommand);
                Assert.NotNull(vm.ToggleFavoriteCommand);
                Assert.NotNull(vm.PaneA.AddBlacklistCommand);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
