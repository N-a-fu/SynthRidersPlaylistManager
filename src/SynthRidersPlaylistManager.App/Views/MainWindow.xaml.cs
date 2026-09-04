using System.Windows;
using SynthRidersPlaylistManager.App.Services;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Infrastructure.EnvironmentDiscovery;
using SynthRidersPlaylistManager.Infrastructure.GameData;

namespace SynthRidersPlaylistManager.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel(new MockLibraryDataSource(), SteamEnvironmentDiscoveryService.CreateDefault(), new WindowsLocationPicker(), new RealLibraryReader());
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeEnvironmentAsync();
    }
}
