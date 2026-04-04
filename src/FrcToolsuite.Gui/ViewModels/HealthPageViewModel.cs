using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
using FrcToolsuite.Core.Health;

namespace FrcToolsuite.Gui.ViewModels;

public partial class HealthCheckItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private HealthSeverity _severity;

    [ObservableProperty]
    private bool _canAutoFix;

    [ObservableProperty]
    private string? _packageId;

    [ObservableProperty]
    private bool _isRepaired;

    public string SeverityIcon => Severity switch
    {
        HealthSeverity.Info => "\u2139",
        HealthSeverity.Warning => "\u26A0",
        HealthSeverity.Error => "\u274C",
        HealthSeverity.Critical => "\u2620",
        _ => "\u2139"
    };

    public string SeverityColor => Severity switch
    {
        HealthSeverity.Info => "#5B8DEF",
        HealthSeverity.Warning => "#E8A838",
        HealthSeverity.Error => "#EF5B5B",
        HealthSeverity.Critical => "#D32F2F",
        _ => "#5B8DEF"
    };
}

public partial class HealthPageViewModel : ObservableObject, IStateExportable
{
    private readonly IHealthChecker? _healthChecker;

    [ObservableProperty]
    private string _title = "Environment Health";

    [ObservableProperty]
    private string _description = "Diagnose your FRC development environment. Verify that Java, compilers, network settings, and tool versions are correctly configured and compatible.";

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private bool _isHealthy;

    public ObservableCollection<HealthCheckItemViewModel> Issues { get; } = new();

    public HealthPageViewModel()
        : this(null)
    {
    }

    public HealthPageViewModel(IHealthChecker? healthChecker)
    {
        _healthChecker = healthChecker;
        if (_healthChecker == null)
        {
            LoadMockData();
        }
    }

    private void LoadMockData()
    {
        // Provide sample data for design-time / test harness
        HasResults = true;
        IsHealthy = false;
        Status = "2 issues found";
        Issues.Add(new HealthCheckItemViewModel
        {
            Description = "Install directory 'C:\\frc' does not exist (no packages installed yet).",
            Severity = HealthSeverity.Info,
            CanAutoFix = false
        });
        Issues.Add(new HealthCheckItemViewModel
        {
            Description = "Package 'wpilib.tools' is missing its install manifest.",
            Severity = HealthSeverity.Warning,
            CanAutoFix = false,
            PackageId = "wpilib.tools"
        });
    }

    [RelayCommand]
    private async Task RunCheckAsync()
    {
        if (_healthChecker == null)
        {
            return;
        }

        IsChecking = true;
        HasResults = false;
        Issues.Clear();
        Status = "Running health checks...";

        try
        {
            var report = await _healthChecker.RunFullCheckAsync();

            HasResults = true;
            IsHealthy = report.IsHealthy;

            if (report.IsHealthy)
            {
                Status = "All checks passed";
            }
            else
            {
                Status = $"{report.Issues.Count} issue{(report.Issues.Count != 1 ? "s" : "")} found";
                foreach (var issue in report.Issues)
                {
                    Issues.Add(new HealthCheckItemViewModel
                    {
                        Description = issue.Description,
                        Severity = issue.Severity,
                        CanAutoFix = issue.CanAutoFix,
                        PackageId = issue.PackageId
                    });
                }
            }
        }
        catch (Exception ex)
        {
            HasResults = true;
            IsHealthy = false;
            Status = "Health check failed";
            Issues.Add(new HealthCheckItemViewModel
            {
                Description = $"Health check failed: {ex.Message}",
                Severity = HealthSeverity.Error,
                CanAutoFix = false
            });
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private async Task RepairIssueAsync(HealthCheckItemViewModel? item)
    {
        if (_healthChecker == null || item == null || !item.CanAutoFix)
        {
            return;
        }

        try
        {
            var issue = new HealthIssue
            {
                Severity = item.Severity,
                Description = item.Description,
                CanAutoFix = item.CanAutoFix,
                PackageId = item.PackageId
            };

            var repaired = await _healthChecker.RepairAsync(issue);
            if (repaired)
            {
                item.IsRepaired = true;
                item.CanAutoFix = false;
            }
        }
        catch
        {
            // Repair failed; leave the issue in place
        }
    }

    public string ExportStateJson()
    {
        var state = new
        {
            Title,
            Description,
            Status,
            IsChecking,
            HasResults,
            IsHealthy,
            IssueCount = Issues.Count
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
