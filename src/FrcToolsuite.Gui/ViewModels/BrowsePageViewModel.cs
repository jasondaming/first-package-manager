using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace FrcToolsuite.Gui.ViewModels;

public partial class PackageViewModel : ObservableObject
{
    public string PackageId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Icon { get; set; } = "\U0001F4E6";

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string? _installError;

    public bool HasUpdate { get; set; }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (IsInstalled || IsInstalling)
        {
            return;
        }

        var pm = App.Services?.GetService<IPackageManager>();
        if (pm == null)
        {
            InstallError = "Package manager is not available.";
            return;
        }

        IsInstalling = true;
        InstallError = null;

        try
        {
            var id = !string.IsNullOrEmpty(PackageId) ? PackageId : Name;
            var plan = await pm.PlanInstallAsync(new[] { id });

            if (plan.Steps.Count == 0)
            {
                IsInstalled = true;
                return;
            }

            await pm.ExecutePlanAsync(plan);
            IsInstalled = true;
        }
        catch (Exception ex)
        {
            InstallError = ex.Message;
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>
    /// First letter of package name for the colored circle icon.
    /// </summary>
    public string IconLetter => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();

    /// <summary>
    /// Deterministic color based on category for the icon circle.
    /// </summary>
    public string IconColor => Category switch
    {
        "Runtime" => "#5B8DEF",
        "IDE" => "#7C5CBF",
        "Tools" => "#E8A838",
        "Libraries" => "#44BB88",
        "Dashboards" => "#EF5B8D",
        _ => "#5B8DEF"
    };

    /// <summary>
    /// Background tint for category badge.
    /// </summary>
    public string CategoryBadgeBackground => Category switch
    {
        "Runtime" => "#EEF1F8",
        "IDE" => "#F5F0FF",
        "Tools" => "#FFF8E8",
        "Libraries" => "#E8F5E9",
        "Dashboards" => "#FDE8F0",
        _ => "#EEF1F8"
    };

    /// <summary>
    /// Text color for category badge.
    /// </summary>
    public string CategoryBadgeForeground => Category switch
    {
        "Runtime" => "#5B8DEF",
        "IDE" => "#7C5CBF",
        "Tools" => "#C88B20",
        "Libraries" => "#2E8B57",
        "Dashboards" => "#D14477",
        _ => "#5B8DEF"
    };
}

public partial class CollectionViewModel : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PackageCount { get; set; }
    public string TotalSize { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isInstalled;

    public ObservableCollection<PackageViewModel> Packages { get; } = new();

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private async Task InstallAllAsync()
    {
        foreach (var pkg in Packages)
        {
            if (!pkg.IsInstalled && !pkg.IsInstalling)
            {
                await pkg.InstallCommand.ExecuteAsync(null);
            }
        }

        IsInstalled = Packages.All(p => p.IsInstalled);
    }

    /// <summary>
    /// First letter of collection name for the colored circle icon.
    /// </summary>
    public string IconLetter => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();
}

public partial class BrowsePageViewModel : ObservableObject, IStateExportable
{
    private readonly IRegistryClient? _registry;
    private readonly IPackageManager? _packageManager;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _filterCategory = "All";

    [ObservableProperty]
    private bool _hideInstalled;

    public ObservableCollection<string> Categories { get; } = new()
    {
        "All", "Runtime", "IDE", "Tools", "Libraries", "Dashboards"
    };

    public ObservableCollection<PackageViewModel> Packages { get; } = new();

    public ObservableCollection<CollectionViewModel> Collections { get; } = new();

    [ObservableProperty]
    private ObservableCollection<PackageViewModel> _filteredPackages = new();

    public BrowsePageViewModel()
        : this(null, null)
    {
    }

    public BrowsePageViewModel(IRegistryClient? registry, IPackageManager? packageManager = null)
    {
        _registry = registry;
        _packageManager = packageManager;

        if (_registry != null)
        {
            _ = LoadPackagesAsync();
        }
        else
        {
            LoadMockData();
        }

        FilteredPackages = new ObservableCollection<PackageViewModel>(Packages);
    }

    public async Task LoadPackagesAsync()
    {
        if (_registry == null)
        {
            return;
        }

        try
        {
            var results = await _registry.SearchAsync();

            // Get installed packages to cross-reference (both new manifest and legacy WPILib)
            var installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_packageManager != null)
            {
                try
                {
                    var installed = await _packageManager.GetInstalledPackagesAsync();
                    foreach (var pkg in installed)
                    {
                        installedIds.Add(pkg.PackageId);
                    }
                }
                catch
                {
                    // Continue without installed state
                }
            }

            // Also detect legacy WPILib installations
            try
            {
                var legacyInstalled = Core.Install.LegacyInstallDetector.DetectInstalledPackages();
                foreach (var id in legacyInstalled)
                {
                    installedIds.Add(id);
                }
            }
            catch
            {
                // Continue without legacy detection
            }

            if (results.Count > 0)
            {
                Packages.Clear();
                foreach (var summary in results)
                {
                    long sizeBytes = 0;
                    if (summary.TotalSize != null)
                    {
                        foreach (var kvp in summary.TotalSize)
                        {
                            if (kvp.Value > sizeBytes)
                            {
                                sizeBytes = kvp.Value;
                            }
                        }
                    }

                    Packages.Add(new PackageViewModel
                    {
                        PackageId = summary.Id,
                        Name = summary.Name,
                        Publisher = summary.PublisherId,
                        Version = summary.Version,
                        Description = summary.Description,
                        Category = NormalizeCategory(summary.Category),
                        Size = FormatBytes(sizeBytes),
                        IsInstalled = installedIds.Contains(summary.Id)
                    });
                }

                ApplyFilters();
            }

            // Load bundles from registry index
            await LoadBundlesAsync(installedIds);
        }
        catch
        {
            // Keep mock data as fallback when registry is unavailable
        }
    }

    private async Task LoadBundlesAsync(HashSet<string> installedIds)
    {
        if (_registry == null)
        {
            return;
        }

        try
        {
            var index = await _registry.FetchRegistryAsync();
            var packageLookup = new Dictionary<string, PackageViewModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var pkg in Packages)
            {
                packageLookup[pkg.PackageId] = pkg;
            }

            Collections.Clear();
            foreach (var bundleRef in index.Bundles)
            {
                try
                {
                    var bundle = await _registry.GetBundleAsync(bundleRef.Id);
                    var collection = new CollectionViewModel
                    {
                        Id = bundle.Id,
                        Name = bundle.Name,
                        Description = bundle.Description,
                        PackageCount = bundle.Packages.Count
                    };

                    long totalBytes = 0;
                    bool allInstalled = true;

                    foreach (var pkgRef in bundle.Packages)
                    {
                        if (packageLookup.TryGetValue(pkgRef.Id, out var existingPkg))
                        {
                            collection.Packages.Add(existingPkg);
                        }
                        else
                        {
                            // Create a placeholder for packages not in the main list
                            collection.Packages.Add(new PackageViewModel
                            {
                                PackageId = pkgRef.Id,
                                Name = pkgRef.Id,
                                Description = pkgRef.Reason ?? string.Empty,
                                IsInstalled = installedIds.Contains(pkgRef.Id)
                            });
                        }

                        var pkg = collection.Packages[^1];
                        if (!pkg.IsInstalled)
                        {
                            allInstalled = false;
                        }
                    }

                    collection.IsInstalled = allInstalled;

                    // Sum sizes from the package summaries in the index
                    foreach (var pkgRef in bundle.Packages)
                    {
                        var summary = index.Packages.FirstOrDefault(
                            p => string.Equals(p.Id, pkgRef.Id, StringComparison.OrdinalIgnoreCase));
                        if (summary?.TotalSize != null)
                        {
                            long maxSize = 0;
                            foreach (var kvp in summary.TotalSize)
                            {
                                if (kvp.Value > maxSize)
                                {
                                    maxSize = kvp.Value;
                                }
                            }

                            totalBytes += maxSize;
                        }
                    }

                    collection.TotalSize = FormatBytes(totalBytes);
                    Collections.Add(collection);
                }
                catch
                {
                    // Skip bundles that fail to load
                }
            }
        }
        catch
        {
            // Continue without bundles if loading fails
        }
    }

    private void LoadMockData()
    {
        Packages.Add(new PackageViewModel
        {
            Name = "WPILib",
            Publisher = "WPI",
            Version = "2026.1.1",
            Description = "The core FRC robot programming framework and libraries.",
            Category = "Runtime",
            Size = "1.2 GB",
            Icon = "\U0001F916"
        });
        Packages.Add(new PackageViewModel
        {
            Name = "FRC Driver Station",
            Publisher = "NI / FIRST",
            Version = "25.0.1",
            Description = "Official driver station for controlling FRC robots.",
            Category = "Tools",
            Size = "320 MB",
            Icon = "\U0001F3AE"
        });
        Packages.Add(new PackageViewModel
        {
            Name = "REVLib",
            Publisher = "REV Robotics",
            Version = "2026.0.2",
            Description = "Vendor libraries for REV Robotics motor controllers and sensors.",
            Category = "Libraries",
            Size = "85 MB",
            Icon = "\u2699\uFE0F"
        });
        Packages.Add(new PackageViewModel
        {
            Name = "PhotonVision",
            Publisher = "PhotonVision",
            Version = "2026.1.0",
            Description = "Open-source computer vision solution for FRC.",
            Category = "Tools",
            Size = "210 MB",
            Icon = "\U0001F4F7"
        });
        Packages.Add(new PackageViewModel
        {
            Name = "CTRE Phoenix",
            Publisher = "CTR Electronics",
            Version = "25.2.1",
            Description = "Phoenix framework for CTRE motor controllers and sensors.",
            Category = "Libraries",
            Size = "150 MB",
            Icon = "\u26A1"
        });
        Packages.Add(new PackageViewModel
        {
            Name = "AdvantageScope",
            Publisher = "Mechanical Advantage",
            Version = "4.1.0",
            Description = "Robot telemetry visualization and log analysis tool.",
            Category = "Dashboards",
            Size = "95 MB",
            Icon = "\U0001F4CA"
        });
        Packages.Add(new PackageViewModel
        {
            Name = "Shuffleboard",
            Publisher = "WPI",
            Version = "2026.1.1",
            Description = "Modular dashboard for FRC robot data display.",
            Category = "Dashboards",
            Size = "110 MB",
            Icon = "\U0001F4CB"
        });
        Packages.Add(new PackageViewModel
        {
            Name = "PathPlanner",
            Publisher = "mjansen4857",
            Version = "2026.0.5",
            Description = "Autonomous path planning and following for FRC robots.",
            Category = "Tools",
            Size = "45 MB",
            Icon = "\U0001F5FA\uFE0F"
        });
        Packages.Add(new PackageViewModel
        {
            Name = "VS Code + FRC Extension",
            Publisher = "WPI / Microsoft",
            Version = "2026.1.0",
            Description = "Visual Studio Code with the WPILib FRC extension pack.",
            Category = "IDE",
            Size = "450 MB",
            Icon = "\U0001F4DD"
        });

        // Mock collections
        var javaStarter = new CollectionViewModel
        {
            Id = "frc-java-starter",
            Name = "FRC Java Starter Kit",
            Description = "Everything you need to get started with FRC Java robot development.",
            PackageCount = 4,
            TotalSize = "1.9 GB"
        };
        javaStarter.Packages.Add(Packages.First(p => p.Name == "WPILib"));
        javaStarter.Packages.Add(Packages.First(p => p.Name == "VS Code + FRC Extension"));
        javaStarter.Packages.Add(Packages.First(p => p.Name == "Shuffleboard"));
        javaStarter.Packages.Add(Packages.First(p => p.Name == "AdvantageScope"));
        Collections.Add(javaStarter);

        var csaToolkit = new CollectionViewModel
        {
            Id = "csa-usb-toolkit",
            Name = "CSA USB Toolkit",
            Description = "Portable toolkit for Control System Advisors at FRC events.",
            PackageCount = 3,
            TotalSize = "680 MB"
        };
        csaToolkit.Packages.Add(Packages.First(p => p.Name == "FRC Driver Station"));
        csaToolkit.Packages.Add(Packages.First(p => p.Name == "AdvantageScope"));
        csaToolkit.Packages.Add(Packages.First(p => p.Name == "CTRE Phoenix"));
        Collections.Add(csaToolkit);
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnFilterCategoryChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnHideInstalledChanged(bool value)
    {
        ApplyFilters();
    }

    [RelayCommand]
    private void SetCategory(string category)
    {
        FilterCategory = category;
    }

    private void ApplyFilters()
    {
        FilteredPackages.Clear();
        foreach (var pkg in Packages)
        {
            bool matchesSearch = string.IsNullOrWhiteSpace(SearchQuery)
                || pkg.Name.Contains(SearchQuery, System.StringComparison.OrdinalIgnoreCase)
                || pkg.Description.Contains(SearchQuery, System.StringComparison.OrdinalIgnoreCase);

            bool matchesCategory = FilterCategory == "All" || pkg.Category == FilterCategory;

            bool matchesInstalled = !HideInstalled || !pkg.IsInstalled;

            if (matchesSearch && matchesCategory && matchesInstalled)
            {
                FilteredPackages.Add(pkg);
            }
        }
    }

    private static string NormalizeCategory(string category)
    {
        return category?.ToLowerInvariant() switch
        {
            "runtime" => "Runtime",
            "ide" => "IDE",
            "ide-extension" => "IDE",
            "tool" => "Tools",
            "library" => "Libraries",
            "framework" => "Libraries",
            "dashboard" => "Dashboards",
            "build-system" => "Tools",
            _ => "Tools"
        };
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

        if (bytes > 0)
        {
            return $"{bytes} B";
        }

        return string.Empty;
    }

    public string ExportStateJson()
    {
        var state = new
        {
            SearchQuery,
            FilterCategory,
            PackageCount = Packages.Count,
            FilteredCount = FilteredPackages.Count,
            CollectionCount = Collections.Count
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
