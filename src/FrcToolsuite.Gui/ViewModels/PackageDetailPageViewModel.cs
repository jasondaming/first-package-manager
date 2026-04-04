using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;

namespace FrcToolsuite.Gui.ViewModels;

public partial class PackageDetailPageViewModel : ObservableObject, IStateExportable
{
    private readonly Action<string>? _navigateCallback;

    public PackageDetailPageViewModel()
        : this(null)
    {
    }

    public PackageDetailPageViewModel(Action<string>? navigateCallback)
    {
        _navigateCallback = navigateCallback;
    }

    [ObservableProperty]
    private string _name = "WPILib";

    [ObservableProperty]
    private string _publisher = "WPI";

    [ObservableProperty]
    private string _version = "2026.1.1";

    [ObservableProperty]
    private string _description = "The core FRC robot programming framework and libraries. Includes the HAL, " +
        "networktables, cscore, and all WPILib Java/C++ libraries needed for robot development. " +
        "This is the foundation package that most other FRC tools depend on.";

    [ObservableProperty]
    private string _category = "Runtime";

    [ObservableProperty]
    private string _license = "BSD-3-Clause";

    [ObservableProperty]
    private string _size = "1.2 GB";

    [ObservableProperty]
    private string _iconLetter = "W";

    [ObservableProperty]
    private string _iconColor = "#5B8DEF";

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private string _releaseNotes = "v2026.1.1 - January 2026\n" +
        "- Updated HAL to support new roboRIO 2.0 firmware\n" +
        "- Improved command-based framework scheduling performance\n" +
        "- Fixed memory leak in NetworkTables client reconnection\n" +
        "- Added new swerve drive kinematics helpers\n" +
        "- Updated vendordeps JSON schema for 2026 season";

    public ObservableCollection<string> Dependencies { get; } = new()
    {
        "Java JDK 17",
        "NI Game Tools 2026",
        "GradleRIO 2026.1.1"
    };

    [RelayCommand]
    private void Install()
    {
        IsInstalled = true;
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigateCallback?.Invoke("Browse");
    }

    public string ExportStateJson()
    {
        var state = new
        {
            Name,
            Publisher,
            Version,
            Category,
            License,
            Size,
            IsInstalled,
            DependencyCount = Dependencies.Count
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
