using System.Runtime.ExceptionServices;
using System.Windows;
using SynthRidersPlaylistManager.App.Services;
using SynthRidersPlaylistManager.App.Views;

namespace SynthRidersPlaylistManager.Tests;

public sealed class StartupLocalizationTests
{
    [Fact]
    public void MainWindowLoadsWithRestoredLocalizationDictionary()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new SynthRidersPlaylistManager.App.App();
                app.InitializeComponent();
                LocalizationService.Initialize();
                var window = new MainWindow();
                window.ApplyTemplate();
                Assert.Equal("Synth Riders Playlist Manager", window.Title);
                window.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
