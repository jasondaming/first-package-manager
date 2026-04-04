using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
using FrcToolsuite.Core.Packages;

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
    private readonly IPackageManager? _packageManager;

    public ObservableCollection<UpdateablePackageViewModel> AvailableUpdates { get; } = new();

    [ObservableProperty]
    private bool _isUpdating;

    public UpdatesPageViewModel()
        : this(null)
    {
    }

    public UpdatesPageViewModel(IPackageManager? packageManager)
    {
        _packageManager = packageManager;

        if (_packageManager != null)
        {
            _ = LoadUpdatesAsync();
        }
        else
        {
            LoadMockData();
        }
    }

    private void LoadMockData()
    {
        AvailableUpdates.Add(new UpdateablePackageViewModel
        {
            Name = "AdvantageScope",
            Publisher = "Mechanical Advantage",
            CurrentVersion = "4.0.0",
            NewVersion = "4.1.0",
            Size = "32 MB",
            Icon = "\U0001F4CA",
            ChangelogSummary = "New 3D field view, improved log parsing performance."
        });
        AvailableUpdates.Add(new UpdateablePackageViewModel
        {
            Name = "REVLib",
            Publisher = "REV Robotics",
            CurrentVersion = "2026.0.1",
            NewVersion = "2026.0.2",
            Size = "12 MB",
            Icon = "\u2699\uFE0F",
            ChangelogSummary = "Bug fix for SPARK MAX encoder reset."
        });
    }

    private async Task LoadUpdatesAsync()
    {
        if (_packageManager == null)
        {
            return;
        }

        try
        {
            var updates = await _packageManager.CheckForUpdatesAsync();
            if (updates.Count > 0)
            {
                AvailableUpdates.Clear();
                foreach (var update in updates)
                {
                    AvailableUpdates.Add(new UpdateablePackageViewModel
                    {
                        Name = update.PackageId,
                        CurrentVersion = update.InstalledVersion,
                        NewVersion = update.AvailableVersion
                    });
                }
            }
            else if (updates.Count == 0)
            {
                // No updates available; clear mock data
                AvailableUpdates.Clear();
            }
        }
        catch
        {
            // Keep mock data as fallback
        }
    }

    [RelayCommand]
    private async Task UpdateAllAsync()
    {
        if (_packageManager == null)
        {
            return;
        }

        IsUpdating = true;
        try
        {
            var plan = await _packageManager.PlanUpdateAsync();
            if (plan.Steps.Count > 0)
            {
                await _packageManager.ExecutePlanAsync(plan);
            }

            AvailableUpdates.Clear();
        }
        catch
        {
            // Update failed; keep items in list so user can retry
        }
        finally
        {
            IsUpdating = false;
        }
    }

    [RelayCommand]
    private async Task UpdatePackageAsync(string packageName)
    {
        if (_packageManager == null)
        {
            return;
        }

        IsUpdating = true;
        try
        {
            var plan = await _packageManager.PlanUpdateAsync(new[] { packageName });
            if (plan.Steps.Count > 0)
            {
                await _packageManager.ExecutePlanAsync(plan);
            }

            var item = AvailableUpdates.FirstOrDefault(u => u.Name == packageName);
            if (item != null)
            {
                AvailableUpdates.Remove(item);
            }
        }
        catch
        {
            // Update failed; keep item in list so user can retry
        }
        finally
        {
            IsUpdating = false;
        }
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
