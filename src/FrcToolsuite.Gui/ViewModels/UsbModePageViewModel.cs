using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
using FrcToolsuite.Core.Offline;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Gui.ViewModels;

public class UsbPackageItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public class UsbPlatformItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public partial class UsbModePageViewModel : ObservableObject, IStateExportable
{
    private readonly IOfflineCacheManager? _cacheManager;
    private readonly IRegistryClient? _registry;

    [ObservableProperty]
    private string _title = "USB Offline Mode";

    [ObservableProperty]
    private string _description = "Download packages to a USB drive for offline installation at competition venues.";

    [ObservableProperty]
    private string _usbPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private double _syncProgress;

    [ObservableProperty]
    private string _syncProgressText = string.Empty;

    [ObservableProperty]
    private string _estimatedSize = "0 MB";

    public ObservableCollection<string> DetectedDrives { get; } = new();

    public ObservableCollection<UsbPackageItem> Packages { get; } = new();

    public ObservableCollection<UsbPlatformItem> Platforms { get; } = new();

    public UsbModePageViewModel()
        : this(null, null)
    {
    }

    public UsbModePageViewModel(IOfflineCacheManager? cacheManager, IRegistryClient? registry)
    {
        _cacheManager = cacheManager;
        _registry = registry;

        InitializePlatforms();

        if (_registry != null)
        {
            _ = LoadPackagesFromRegistryAsync();
        }
        else
        {
            LoadMockData();
        }

        RefreshDrives();
    }

    private void InitializePlatforms()
    {
        Platforms.Add(new UsbPlatformItem { Id = "windows-x64", Label = "Windows x64", IsSelected = true });
        Platforms.Add(new UsbPlatformItem { Id = "macos-arm64", Label = "macOS ARM64" });
        Platforms.Add(new UsbPlatformItem { Id = "macos-x64", Label = "macOS x64" });
        Platforms.Add(new UsbPlatformItem { Id = "linux-x64", Label = "Linux x64" });
        Platforms.Add(new UsbPlatformItem { Id = "linux-arm64", Label = "Linux ARM64" });

        foreach (var platform in Platforms)
        {
            platform.PropertyChanged += (_, _) => UpdateEstimatedSize();
        }
    }

    private void LoadMockData()
    {
        Packages.Add(new UsbPackageItem { Id = "wpilib.jdk", Name = "JDK 17.0.16+8", Size = "191 MB", SizeBytes = 191_000_000 });
        Packages.Add(new UsbPackageItem { Id = "wpilib.vscode", Name = "VS Code 1.105.1", Size = "131 MB", SizeBytes = 131_000_000 });
        Packages.Add(new UsbPackageItem { Id = "wpilib.gradlerio", Name = "GradleRIO 2026.2.1", Size = "50 MB", SizeBytes = 50_000_000 });
        Packages.Add(new UsbPackageItem { Id = "ctre.phoenix6", Name = "CTRE Phoenix 6", Size = "150 MB", SizeBytes = 150_000_000 });
        Packages.Add(new UsbPackageItem { Id = "rev.revlib", Name = "REVLib", Size = "85 MB", SizeBytes = 85_000_000 });
        Packages.Add(new UsbPackageItem { Id = "wpilib.advantagescope", Name = "AdvantageScope", Size = "95 MB", SizeBytes = 95_000_000 });

        foreach (var pkg in Packages)
        {
            pkg.PropertyChanged += (_, _) => UpdateEstimatedSize();
        }

        UpdateEstimatedSize();
    }

    private async Task LoadPackagesFromRegistryAsync()
    {
        try
        {
            var index = await _registry!.FetchRegistryAsync();
            foreach (var pkg in index.Packages)
            {
                long size = pkg.TotalSize.Values.DefaultIfEmpty(0).Max();
                var item = new UsbPackageItem
                {
                    Id = pkg.Id,
                    Name = pkg.Name,
                    Size = FormatBytes(size),
                    SizeBytes = size
                };
                item.PropertyChanged += (_, _) => UpdateEstimatedSize();
                Packages.Add(item);
            }

            UpdateEstimatedSize();
        }
        catch
        {
            LoadMockData();
        }
    }

    [RelayCommand]
    private void RefreshDrives()
    {
        DetectedDrives.Clear();
        try
        {
            var drives = OfflineCacheManager.DetectUsbDrives();
            foreach (var drive in drives)
            {
                DetectedDrives.Add($"{drive.Name} ({FormatBytes(drive.AvailableFreeSpace)} free)");
            }

            // Auto-select the first detected drive
            if (drives.Count > 0 && string.IsNullOrWhiteSpace(UsbPath))
            {
                UsbPath = drives[0].Name;
            }
        }
        catch
        {
            // Drive detection may not work on all platforms
        }

        if (DetectedDrives.Count == 0)
        {
            DetectedDrives.Add("No USB drives detected - type a path below");
        }
    }

    [RelayCommand]
    private void SelectDrive(string driveDisplay)
    {
        // Extract drive letter from "D:\ (15.2 GB free)" format
        var spaceIndex = driveDisplay.IndexOf(' ');
        if (spaceIndex > 0)
        {
            UsbPath = driveDisplay[..spaceIndex];
        }
        else
        {
            UsbPath = driveDisplay;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var pkg in Packages)
        {
            pkg.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var pkg in Packages)
        {
            pkg.IsSelected = false;
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SyncToUsbAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(UsbPath))
        {
            StatusMessage = "Please enter a USB drive path.";
            return;
        }

        var selectedPackages = Packages.Where(p => p.IsSelected).Select(p => p.Id).ToList();
        if (selectedPackages.Count == 0)
        {
            StatusMessage = "Please select at least one package.";
            return;
        }

        var selectedPlatforms = Platforms.Where(p => p.IsSelected).Select(p => p.Id).ToList();
        if (selectedPlatforms.Count == 0)
        {
            StatusMessage = "Please select at least one platform.";
            return;
        }

        IsSyncing = true;
        SyncProgress = 0;
        StatusMessage = "Syncing packages to USB...";

        try
        {
            if (_cacheManager != null)
            {
                var progress = new Progress<OfflineSyncProgress>(p =>
                {
                    if (p.TotalItems > 0)
                    {
                        SyncProgress = (double)p.CompletedItems / p.TotalItems * 100;
                    }

                    SyncProgressText = $"{p.CompletedItems}/{p.TotalItems} - {p.CurrentItem}";
                });

                await _cacheManager.ExportToUsbAsync(
                    UsbPath,
                    selectedPackages,
                    selectedPlatforms,
                    progress,
                    ct);

                StatusMessage = $"Sync complete! {selectedPackages.Count} packages written to {UsbPath}";
            }
            else
            {
                // Mock sync for design-time / test harness
                for (int i = 0; i <= 100; i += 10)
                {
                    ct.ThrowIfCancellationRequested();
                    SyncProgress = i;
                    SyncProgressText = $"Syncing package {i / 10 + 1} of 10...";
                    await Task.Delay(200, ct);
                }

                StatusMessage = $"Sync complete! {selectedPackages.Count} packages written to {UsbPath}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Sync cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync failed: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
            SyncProgress = 0;
            SyncProgressText = string.Empty;
        }
    }

    private void UpdateEstimatedSize()
    {
        long totalBytes = 0;
        int selectedPlatformCount = Platforms.Count(p => p.IsSelected);
        if (selectedPlatformCount == 0)
        {
            selectedPlatformCount = 1;
        }

        foreach (var pkg in Packages.Where(p => p.IsSelected))
        {
            totalBytes += pkg.SizeBytes;
        }

        // Rough estimate: multiply by platform count (simplified)
        totalBytes = (long)(totalBytes * (selectedPlatformCount * 0.8));
        EstimatedSize = FormatBytes(totalBytes);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824)
        {
            return $"{bytes / 1_073_741_824.0:F1} GB";
        }

        if (bytes >= 1_048_576)
        {
            return $"{bytes / 1_048_576.0:F0} MB";
        }

        if (bytes >= 1024)
        {
            return $"{bytes / 1024.0:F0} KB";
        }

        return $"{bytes} B";
    }

    public string ExportStateJson()
    {
        var state = new
        {
            Title,
            Description,
            UsbPath,
            StatusMessage,
            IsSyncing,
            EstimatedSize,
            PackageCount = Packages.Count,
            SelectedPackageCount = Packages.Count(p => p.IsSelected),
            PlatformCount = Platforms.Count,
            SelectedPlatformCount = Platforms.Count(p => p.IsSelected),
            DetectedDriveCount = DetectedDrives.Count
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
