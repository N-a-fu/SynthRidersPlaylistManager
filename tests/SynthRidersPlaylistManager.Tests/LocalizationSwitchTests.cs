using System.ComponentModel;
using SynthRidersPlaylistManager.App.Services;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.Tests;

[Collection(WpfTestCollection.Name)]
public sealed class LocalizationSwitchTests(WpfTestFixture wpf)
{
    [Fact]
    public void SwitchesBothPanesImmediatelyPreservesStateAndPersistsLanguage()
    {
        wpf.Run(() =>
        {
                LocalizationService.Apply("ja-JP", false);
                using var vm = new MainViewModel(new MockLibraryDataSource());
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
                var now = DateTimeOffset.Now;
                var success = new EnvironmentDiscoveryResult(
                    [new(DataLocationKind.GameRoot, "fixture", DataLocationSource.AutoDetected, DataLocationStatus.Available, now, "fixture", false),
                     new(DataLocationKind.CustomSongs, null, DataLocationSource.NotResolved, DataLocationStatus.NotConfigured, now, "fixture", false)],
                    "Environment.Summary.Success", now);
                var unavailable = new EnvironmentDiscoveryResult([], "Environment.Summary.Unavailable", now);
                var rejected = new EnvironmentDiscoveryResult([], "Environment.Summary.ManualOverrideRejected", now);

                Assert.Equal("Steam版Synth Ridersを検出しました。Data Location 1/2件が利用可能です。", MainViewModel.LocalizeEnvironmentSummary(success));
                Assert.Equal("Synth Ridersのデータを利用できません。\n設定を確認して、再読み込みしてください。", MainViewModel.LocalizeEnvironmentSummary(unavailable));
                Assert.Equal("手動指定Pathを検証できなかったため保存していません。", MainViewModel.LocalizeEnvironmentSummary(rejected));

                vm.ToggleLanguageCommand.Execute(null);
                Assert.Equal("en-US", LocalizationService.CurrentLanguage);
                Assert.Equal("All Favorites", vm.FavoritesNavigation[0].Label);
                Assert.Equal("All Songs", vm.SmartNavigation[0].Label);
                Assert.Equal("Blacklist", vm.SmartNavigation.Last().Label);
                Assert.Equal("↻ Reload", MainViewModel.UiText("Action.Rescan"));
                Assert.Equal("Normally, Synth Riders locations are detected automatically.\nOnly specify the paths below manually if automatic detection does not work correctly.", MainViewModel.UiText("Settings.PathDetectionHelp"));
                Assert.Equal("Steam Synth Riders detected. 1 of 2 data locations are available.", MainViewModel.LocalizeEnvironmentSummary(success));
                Assert.Equal("Synth Riders data is unavailable.\nCheck the settings and reload.", MainViewModel.LocalizeEnvironmentSummary(unavailable));
                Assert.Equal("The manually specified path could not be validated and was not saved.", MainViewModel.LocalizeEnvironmentSummary(rejected));
                Assert.Equal([NavigationFilter.Favorites, NavigationFilter.UnsortedFavorites], vm.FavoritesNavigation.Select(item => item.Filter));
                Assert.Equal([NavigationFilter.AllSongs, NavigationFilter.Unassigned, NavigationFilter.Blacklist], vm.SmartNavigation.Select(item => item.Filter));
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
                Assert.Equal("↻ 再読み込み", MainViewModel.UiText("Action.Rescan"));
                Assert.Equal("通常はSynth Ridersの保存場所を自動検出します。\n正しく認識されない場合のみ、下の項目を手動で指定してください。", MainViewModel.UiText("Settings.PathDetectionHelp"));
                Assert.Equal("ja-JP", UiSettingsStore.CreateDefault().Load().Language);

                // Existing behavior remains enum/identity-driven, never label-driven.
                Assert.True(vm.PaneA.IsPlaylist);
                Assert.NotNull(vm.PaneB.DropCommand);
                Assert.NotNull(vm.PaneA.RemoveFromCurrentPlaylistCommand);
                Assert.NotNull(vm.ToggleFavoriteCommand);
                Assert.NotNull(vm.PaneA.AddBlacklistCommand);
        });
    }
}
