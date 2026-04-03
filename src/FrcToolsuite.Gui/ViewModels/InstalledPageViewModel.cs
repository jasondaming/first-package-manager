using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using FrcToolsuite.Core;

namespace FrcToolsuite.Gui.ViewModels;

public class InstalledPackageViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InstalledDate { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Icon { get; set; } = "\U0001F4E6";
}

public partial class InstalledPageViewModel : ObservableObject, IStateExportable
{
    public ObservableCollection<InstalledPackageViewModel> InstalledPackages { get; } = new()
    {
        new InstalledPackageViewModel
        {
            Name = "WPILib",
            Publisher = "WPI",
            Version = "2026.1.1",
            InstalledDate = "2026-01-15",
            Size = "1.2 GB",
            Icon = "\U0001F916"
        },
        new InstalledPackageViewModel
        {
            Name = "FRC Driver Station",
            Publisher = "NI / FIRST",
            Version = "25.0.1",
            InstalledDate = "2026-01-15",
            Size = "320 MB",
            Icon = "\U0001F3AE"
        },
        new InstalledPackageViewModel
        {
            Name = "AdvantageScope",
            Publisher = "Mechanical Advantage",
            Version = "4.0.0",
            InstalledDate = "2026-02-10",
            Size = "95 MB",
            Icon = "\U0001F4CA"
        }
    };

    [ObservableProperty]
    private string _totalSize = "1.6 GB";

    public string ExportStateJson()
    {
        var state = new
        {
            InstalledCount = InstalledPackages.Count,
            TotalSize
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
