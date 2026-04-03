using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;

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
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _filterCategory = "All";

    public ObservableCollection<string> Categories { get; } = new()
    {
        "All", "Runtime", "IDE", "Tools", "Libraries", "Dashboards"
    };

    public ObservableCollection<PackageViewModel> Packages { get; } = new()
    {
        new PackageViewModel
        {
            Name = "WPILib",
            Publisher = "WPI",
            Version = "2026.1.1",
            Description = "The core FRC robot programming framework and libraries.",
            Category = "Runtime",
            Size = "1.2 GB",
            Icon = "\U0001F916"
        },
        new PackageViewModel
        {
            Name = "FRC Driver Station",
            Publisher = "NI / FIRST",
            Version = "25.0.1",
            Description = "Official driver station for controlling FRC robots.",
            Category = "Tools",
            Size = "320 MB",
            Icon = "\U0001F3AE"
        },
        new PackageViewModel
        {
            Name = "REVLib",
            Publisher = "REV Robotics",
            Version = "2026.0.2",
            Description = "Vendor libraries for REV Robotics motor controllers and sensors.",
            Category = "Libraries",
            Size = "85 MB",
            Icon = "\u2699\uFE0F"
        },
        new PackageViewModel
        {
            Name = "PhotonVision",
            Publisher = "PhotonVision",
            Version = "2026.1.0",
            Description = "Open-source computer vision solution for FRC.",
            Category = "Tools",
            Size = "210 MB",
            Icon = "\U0001F4F7"
        },
        new PackageViewModel
        {
            Name = "CTRE Phoenix",
            Publisher = "CTR Electronics",
            Version = "25.2.1",
            Description = "Phoenix framework for CTRE motor controllers and sensors.",
            Category = "Libraries",
            Size = "150 MB",
            Icon = "\u26A1"
        },
        new PackageViewModel
        {
            Name = "AdvantageScope",
            Publisher = "Mechanical Advantage",
            Version = "4.1.0",
            Description = "Robot telemetry visualization and log analysis tool.",
            Category = "Dashboards",
            Size = "95 MB",
            Icon = "\U0001F4CA"
        },
        new PackageViewModel
        {
            Name = "Shuffleboard",
            Publisher = "WPI",
            Version = "2026.1.1",
            Description = "Modular dashboard for FRC robot data display.",
            Category = "Dashboards",
            Size = "110 MB",
            Icon = "\U0001F4CB"
        },
        new PackageViewModel
        {
            Name = "PathPlanner",
            Publisher = "mjansen4857",
            Version = "2026.0.5",
            Description = "Autonomous path planning and following for FRC robots.",
            Category = "Tools",
            Size = "45 MB",
            Icon = "\U0001F5FA\uFE0F"
        },
        new PackageViewModel
        {
            Name = "VS Code + FRC Extension",
            Publisher = "WPI / Microsoft",
            Version = "2026.1.0",
            Description = "Visual Studio Code with the WPILib FRC extension pack.",
            Category = "IDE",
            Size = "450 MB",
            Icon = "\U0001F4DD"
        }
    };

    [ObservableProperty]
    private ObservableCollection<PackageViewModel> _filteredPackages = new();

    public BrowsePageViewModel()
    {
        FilteredPackages = new ObservableCollection<PackageViewModel>(Packages);
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
