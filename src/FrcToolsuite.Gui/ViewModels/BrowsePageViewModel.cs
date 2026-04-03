using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Gui.ViewModels;

public class PackageViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Icon { get; set; } = "\U0001F4E6";
    public bool IsInstalled { get; set; }
    public bool HasUpdate { get; set; }

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

public partial class BrowsePageViewModel : ObservableObject, IStateExportable
{
    private readonly IRegistryClient? _registry;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _filterCategory = "All";

    public ObservableCollection<string> Categories { get; } = new()
    {
        "All", "Runtime", "IDE", "Tools", "Libraries", "Dashboards"
    };

    public ObservableCollection<PackageViewModel> Packages { get; } = new();

    [ObservableProperty]
    private ObservableCollection<PackageViewModel> _filteredPackages = new();

    public BrowsePageViewModel()
        : this(null)
    {
    }

    public BrowsePageViewModel(IRegistryClient? registry)
    {
        _registry = registry;
        LoadMockData();
        FilteredPackages = new ObservableCollection<PackageViewModel>(Packages);

        if (_registry != null)
        {
            _ = LoadPackagesAsync();
        }
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
                        Name = summary.Name,
                        Publisher = summary.PublisherId,
                        Version = summary.Version,
                        Description = summary.Description,
                        Category = NormalizeCategory(summary.Category),
                        Size = FormatBytes(sizeBytes)
                    });
                }

                ApplyFilters();
            }
        }
        catch
        {
            // Keep mock data as fallback when registry is unavailable
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
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnFilterCategoryChanged(string value)
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

            if (matchesSearch && matchesCategory)
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
            FilteredCount = FilteredPackages.Count
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
