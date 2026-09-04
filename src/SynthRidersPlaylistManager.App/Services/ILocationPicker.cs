using Microsoft.Win32;
using System.IO;
using SynthRidersPlaylistManager.App.ViewModels;
using SynthRidersPlaylistManager.Core.Models;

namespace SynthRidersPlaylistManager.App.Services;

public interface ILocationPicker
{
    string? Pick(DataLocationKind kind, string? currentPath);
}

public sealed class WindowsLocationPicker : ILocationPicker
{
    public string? Pick(DataLocationKind kind, string? currentPath)
    {
        if (kind is DataLocationKind.Favorites or DataLocationKind.SynthDatabase)
        {
            var dialog = new OpenFileDialog
            {
                Title = kind == DataLocationKind.Favorites ? "favorites.binを選択" : "SynthDBを選択",
                CheckFileExists = true,
                FileName = File.Exists(currentPath) ? currentPath : ""
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        var folderDialog = new OpenFolderDialog
        {
            Title = $"{EnvironmentLocationViewModel.DisplayNameFor(kind)}のフォルダを選択",
            InitialDirectory = Directory.Exists(currentPath) ? currentPath : null
        };
        return folderDialog.ShowDialog() == true ? folderDialog.FolderName : null;
    }
}
