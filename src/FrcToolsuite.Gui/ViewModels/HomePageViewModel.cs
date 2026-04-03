using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using FrcToolsuite.Core;
using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Gui.ViewModels;

public class FeaturedBundle
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "\U0001F4E6";
    public int PackageCount { get; set; }
}

public partial class HomePageViewModel : ObservableObject, IStateExportable
{
    private readonly IPackageManager? _packageManager;
    private readonly IRegistryClient? _registry;

    [ObservableProperty]
    private string _welcomeMessage = "Welcome to FIRST Package Manager";

    [ObservableProperty]
    private int _installedCount;

    [ObservableProperty]
    private int _updateCount;

    [ObservableProperty]
    private string _diskUsage = "0 MB";

    public ObservableCollection<FeaturedBundle> FeaturedBundles { get; } = new();

    public HomePageViewModel()
        : this(null, null)
    {
    }

    public HomePageViewModel(IPackageManager? packageManager, IRegistryClient? registry)
    {
        _packageManager = packageManager;
        _registry = registry;
        LoadMockData();

        if (_packageManager != null || _registry != null)
        {
            _ = LoadRealDataAsync();
        }
    }

    private void LoadMockData()
    {
        InstalledCount = 3;
        UpdateCount = 2;
        DiskUsage = "1.6 GB";

        FeaturedBundles.Add(new FeaturedBundle
        {
            Name = "FRC Starter Kit",
            Description = "Everything you need to start FRC development: WPILib, Driver Station, and more.",
            Icon = "\U0001F680",
            PackageCount = 8
        });
        FeaturedBundles.Add(new FeaturedBundle
        {
            Name = "Vision Processing",
            Description = "PhotonVision, Limelight tools, and OpenCV libraries for robot vision.",
            Icon = "\U0001F4F7",
            PackageCount = 5
        });
        FeaturedBundles.Add(new FeaturedBundle
        {
            Name = "Dashboard Essentials",
            Description = "Shuffleboard, Glass, AdvantageScope, and Elastic dashboard.",
            Icon = "\U0001F4CA",
            PackageCount = 4
        });
        FeaturedBundles.Add(new FeaturedBundle
        {
            Name = "Simulation Tools",
            Description = "Physics simulation, field visualization, and testing frameworks.",
            Icon = "\U0001F9EA",
            PackageCount = 3
        });
    }

    private async Task LoadRealDataAsync()
    {
        try
        {
            if (_packageManager != null)
            {
                var installed = await _packageManager.GetInstalledPackagesAsync();
                if (installed.Count > 0)
                {
                    InstalledCount = installed.Count;

                    long totalBytes = 0;
                    foreach (var pkg in installed)
                    {
                        if (Directory.Exists(pkg.InstallPath))
                        {
                            try
                            {
                                var dirInfo = new DirectoryInfo(pkg.InstallPath);
                                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                                {
                                    totalBytes += file.Length;
                                }
                            }
                            catch
                            {
                                // Skip directories we cannot enumerate
                            }
                        }
                    }

                    DiskUsage = FormatBytes(totalBytes);
                }

                var updates = await _packageManager.CheckForUpdatesAsync();
                if (updates.Count >= 0)
                {
                    UpdateCount = updates.Count;
                }
            }
        }
        catch
        {
            // Keep mock data as fallback
        }
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
            WelcomeMessage,
            FeaturedBundleCount = FeaturedBundles.Count,
            InstalledCount,
            UpdateCount,
            DiskUsage
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
