using SynthRidersPlaylistManager.App.Services;
using System.Windows;

namespace SynthRidersPlaylistManager.Tests;

[Collection(WpfTestCollection.Name)]
public sealed class StartupLocalizationTests(WpfTestFixture wpf)
{
    [Fact]
    public void MainWindowLoadsWithRestoredLocalizationDictionary()
    {
        wpf.Run(() =>
        {
            LocalizationService.Initialize();
            Assert.Equal("Synth Riders Playlist Manager", Application.Current.TryFindResource("App.Title"));
        });
    }
}
