using System.Windows;
using SynthRidersPlaylistManager.App.Services;

namespace SynthRidersPlaylistManager.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        LocalizationService.Initialize();
        base.OnStartup(e);
    }
}
