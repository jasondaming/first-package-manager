using FrcToolsuite.Core.Configuration;
using FrcToolsuite.Core.Packages;

namespace FrcToolsuite.Core.Health;

public class HealthChecker : IHealthChecker
{
    private readonly IPackageManager _packageManager;
    private readonly ISettingsProvider _settingsProvider;

    public HealthChecker(IPackageManager packageManager, ISettingsProvider settingsProvider)
    {
        _packageManager = packageManager;
        _settingsProvider = settingsProvider;
    }

    public async Task<HealthReport> RunFullCheckAsync(CancellationToken ct = default)
    {
        var report = new HealthReport();
        var settings = await _settingsProvider.LoadAsync(ct);

        // Check install directory exists
        if (!Directory.Exists(settings.InstallDirectory))
        {
            report.Issues.Add(new HealthIssue
            {
                Severity = HealthSeverity.Info,
                Description = $"Install directory '{settings.InstallDirectory}' does not exist (no packages installed yet).",
                CanAutoFix = false
            });
            return report;
        }

        // Check each installed package has a valid manifest
        var packages = await _packageManager.GetInstalledPackagesAsync(ct);
        foreach (var pkg in packages)
        {
            ct.ThrowIfCancellationRequested();
            var manifestPath = Path.Combine(pkg.InstallPath, ".install-manifest.json");
            if (!File.Exists(manifestPath))
            {
                report.Issues.Add(new HealthIssue
                {
                    Severity = HealthSeverity.Warning,
                    Description = $"Package '{pkg.PackageId}' is missing its install manifest.",
                    CanAutoFix = false,
                    PackageId = pkg.PackageId
                });
            }
            else if (!Directory.Exists(pkg.InstallPath))
            {
                report.Issues.Add(new HealthIssue
                {
                    Severity = HealthSeverity.Error,
                    Description = $"Package '{pkg.PackageId}' install directory is missing.",
                    CanAutoFix = true,
                    PackageId = pkg.PackageId
                });
            }
        }

        return report;
    }

    public Task<HealthReport> RunCheckAsync(string checkName, CancellationToken ct = default)
    {
        // For now, delegate to full check
        return RunFullCheckAsync(ct);
    }

    public Task<bool> RepairAsync(HealthIssue issue, CancellationToken ct = default)
    {
        // Placeholder: repair is not yet implemented
        return Task.FromResult(false);
    }
}
