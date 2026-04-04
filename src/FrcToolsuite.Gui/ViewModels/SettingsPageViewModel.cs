using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;

namespace FrcToolsuite.Gui.ViewModels;

public partial class SettingsPageViewModel : ObservableObject, IStateExportable
{
    [ObservableProperty]
    private string _installPath = @"C:\frc";

    [ObservableProperty]
    private bool _autoCheckUpdates = true;

    [ObservableProperty]
    private bool _useBetaChannel;

    [ObservableProperty]
    private string _proxyUrl = string.Empty;

    [ObservableProperty]
    private int _maxConcurrentDownloads = 3;

    [ObservableProperty]
    private bool _keepInstallerCache = true;

    [RelayCommand]
    private async Task BrowseInstallPathAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow?.StorageProvider is { } storage)
        {
            var result = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Install Directory",
                AllowMultiple = false
            });
            if (result.Count > 0)
            {
                InstallPath = result[0].Path.LocalPath;
            }
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        var cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".frctoolsuite", "cache");
        if (Directory.Exists(cachePath))
        {
            Directory.Delete(cachePath, true);
            Directory.CreateDirectory(cachePath);
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        InstallPath = @"C:\frc";
        AutoCheckUpdates = true;
        UseBetaChannel = false;
        ProxyUrl = string.Empty;
        MaxConcurrentDownloads = 3;
        KeepInstallerCache = true;
    }

    public string ExportStateJson()
    {
        var state = new
        {
            InstallPath,
            AutoCheckUpdates,
            UseBetaChannel,
            ProxyUrl,
            MaxConcurrentDownloads,
            KeepInstallerCache
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
