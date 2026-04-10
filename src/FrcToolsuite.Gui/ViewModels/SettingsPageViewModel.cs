using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
using FrcToolsuite.Core.Install;

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

    [ObservableProperty]
    private bool _preferAdvancedMode;

    [ObservableProperty]
    private string _selectedTheme = "System";

    [ObservableProperty]
    private bool _hasPreviousYears;

    public ObservableCollection<PreviousYearItem> PreviousYears { get; } = new();

    public ObservableCollection<string> ThemeOptions { get; } = new() { "System", "Light", "Dark" };

    public SettingsPageViewModel()
    {
        DetectPreviousYears();
    }

    partial void OnSelectedThemeChanged(string value)
    {
        App.SetTheme(value);
    }

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

    private void DetectPreviousYears()
    {
        PreviousYears.Clear();
        var detected = LegacyYearDetector.DetectPreviousYears();
        foreach (var install in detected)
        {
            PreviousYears.Add(new PreviousYearItem
            {
                Year = install.Year,
                Path = install.Path,
                SizeBytes = install.SizeBytes
            });
        }

        HasPreviousYears = PreviousYears.Count > 0;
    }

    [RelayCommand]
    private async Task UninstallYearAsync(PreviousYearItem item)
    {
        if (item.IsRemoving || item.IsRemoved)
        {
            return;
        }

        item.IsRemoving = true;
        item.StatusText = "Removing...";

        try
        {
            var progress = new Progress<string>(message =>
            {
                item.StatusText = message;
            });

            await LegacyYearDetector.UninstallYearAsync(item.Year, progress);

            item.IsRemoved = true;
            item.IsRemoving = false;
            item.StatusText = "Removed";

            if (PreviousYears.All(y => y.IsRemoved))
            {
                HasPreviousYears = false;
            }
        }
        catch (Exception ex)
        {
            item.IsRemoving = false;
            item.StatusText = $"Failed: {ex.Message}";
        }
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
        PreferAdvancedMode = false;
        SelectedTheme = "System";
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
            KeepInstallerCache,
            PreferAdvancedMode,
            SelectedTheme,
            HasPreviousYears,
            PreviousYears = PreviousYears.Select(y => new
            {
                y.Year,
                y.Path,
                y.SizeBytes,
                y.SizeDisplay,
                y.IsRemoved
            }).ToArray()
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
