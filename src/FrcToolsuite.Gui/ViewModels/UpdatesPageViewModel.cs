using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;

namespace FrcToolsuite.Gui.ViewModels;

public class UpdateablePackageViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string NewVersion { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Icon { get; set; } = "\U0001F4E6";
    public string ChangelogSummary { get; set; } = string.Empty;
}

public partial class UpdatesPageViewModel : ObservableObject, IStateExportable
{
    public ObservableCollection<UpdateablePackageViewModel> AvailableUpdates { get; } = new()
    {
        new UpdateablePackageViewModel
        {
            Name = "AdvantageScope",
            Publisher = "Mechanical Advantage",
            CurrentVersion = "4.0.0",
            NewVersion = "4.1.0",
            Size = "32 MB",
            Icon = "\U0001F4CA",
            ChangelogSummary = "New 3D field view, improved log parsing performance."
        },
        new UpdateablePackageViewModel
        {
            Name = "REVLib",
            Publisher = "REV Robotics",
            CurrentVersion = "2026.0.1",
            NewVersion = "2026.0.2",
            Size = "12 MB",
            Icon = "\u2699\uFE0F",
            ChangelogSummary = "Bug fix for SPARK MAX encoder reset."
        }
    };

    [ObservableProperty]
    private bool _isUpdating;

    [RelayCommand]
    private void UpdateAll()
    {
        IsUpdating = true;
        // In a real implementation this would trigger actual updates
    }

    [RelayCommand]
    private void UpdatePackage(string packageName)
    {
        // Placeholder for single-package update
    }

    public string ExportStateJson()
    {
        var state = new
        {
            AvailableUpdateCount = AvailableUpdates.Count,
            IsUpdating
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
