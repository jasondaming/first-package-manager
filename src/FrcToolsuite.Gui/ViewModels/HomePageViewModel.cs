using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

public class QuickActionItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PackageCount { get; set; }
    public string BundleId { get; set; } = string.Empty;
}

public class RecentUpdateItem
{
    public string PackageName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string UpdatedAgo { get; set; } = string.Empty;
}

public partial class HomePageViewModel : ObservableObject, IStateExportable
{
    private readonly IPackageManager? _packageManager;
    private readonly IRegistryClient? _registry;
    private readonly Action<string>? _navigateCallback;

    [ObservableProperty]
    private string _welcomeMessage = "Welcome to FIRST Package Manager";

    [ObservableProperty]
    private int _installedCount;

    [ObservableProperty]
    private int _updateCount;

    [ObservableProperty]
    private string _diskUsage = "0 MB";

    public ObservableCollection<FeaturedBundle> FeaturedBundles { get; } = new();

    public ObservableCollection<QuickActionItem> QuickActions { get; } = new();

    public ObservableCollection<RecentUpdateItem> RecentUpdates { get; } = new();

    public HomePageViewModel()
        : this(null, null, null)
    {
    }

    public HomePageViewModel(IPackageManager? packageManager, IRegistryClient? registry, Action<string>? navigateCallback = null)
    {
        _packageManager = packageManager;
        _registry = registry;
        _navigateCallback = navigateCallback;
        LoadMockData();

        if (_packageManager != null || _registry != null)
        {
            _ = LoadRealDataAsync();
        }
    }

    [RelayCommand]
    private void Navigate(string destination)
    {
        _navigateCallback?.Invoke(destination);
    }

    private void LoadMockData()
    {
        InstalledCount = 3;
        UpdateCount = 2;
        DiskUsage = "1.6 GB";

        QuickActions.Add(new QuickActionItem
        {
            Name = "FRC Java Starter",
            Description = "JDK, VS Code, WPILib, tools, and dashboards",
            PackageCount = 12,
            BundleId = "frc-java-starter-2026"
        });
        QuickActions.Add(new QuickActionItem
        {
            Name = "FRC C++ Starter",
            Description = "VS Code, WPILib, tools, and dashboards",
            PackageCount = 10,
            BundleId = "frc-cpp-starter-2026"
        });
        QuickActions.Add(new QuickActionItem
        {
            Name = "CSA USB Toolkit",
            Description = "Diagnostic tools for event support",
            PackageCount = 6,
            BundleId = "csa-usb-toolkit-2026"
        });

        RecentUpdates.Add(new RecentUpdateItem
        {
            PackageName = "GradleRIO",
            Version = "2026.2.1",
            UpdatedAgo = "2 days ago"
        });
        RecentUpdates.Add(new RecentUpdateItem
        {
            PackageName = "CTRE Phoenix 6",
            Version = "26.1.0",
            UpdatedAgo = "5 days ago"
        });
        RecentUpdates.Add(new RecentUpdateItem
        {
            PackageName = "AdvantageScope",
            Version = "v26.0.0",
            UpdatedAgo = "1 week ago"
        });
        RecentUpdates.Add(new RecentUpdateItem
        {
            PackageName = "REVLib",
            Version = "2026.0.1",
            UpdatedAgo = "1 week ago"
        });
        RecentUpdates.Add(new RecentUpdateItem
        {
            PackageName = "Elastic Dashboard",
            Version = "v2026.1.1",
            UpdatedAgo = "2 weeks ago"
        });

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
            QuickActionCount = QuickActions.Count,
            RecentUpdateCount = RecentUpdates.Count,
            InstalledCount,
            UpdateCount,
            DiskUsage
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
