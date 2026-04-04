using System.Text.Json;
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
    private void BrowseInstallPath()
    {
        // Placeholder: would open a folder picker dialog
    }

    [RelayCommand]
    private void ClearCache()
    {
        // Placeholder: would clear the download/installer cache
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
