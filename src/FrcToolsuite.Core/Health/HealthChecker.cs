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
                CanAutoFix = true
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

    public async Task<bool> RepairAsync(HealthIssue issue, CancellationToken ct = default)
    {
        if (!issue.CanAutoFix)
        {
            return false;
        }

        try
        {
            var settings = await _settingsProvider.LoadAsync(ct);

            // Repair: missing install directory - create it
            if (issue.Description.Contains("install directory", StringComparison.OrdinalIgnoreCase)
                && issue.Description.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(settings.InstallDirectory);
                return Directory.Exists(settings.InstallDirectory);
            }

            // Repair: missing package install directory - create it
            if (issue.PackageId != null
                && issue.Description.Contains("install directory is missing", StringComparison.OrdinalIgnoreCase))
            {
                var packages = await _packageManager.GetInstalledPackagesAsync(ct);
                var pkg = packages.FirstOrDefault(p =>
                    string.Equals(p.PackageId, issue.PackageId, StringComparison.OrdinalIgnoreCase));
                if (pkg != null && !string.IsNullOrEmpty(pkg.InstallPath))
                {
                    Directory.CreateDirectory(pkg.InstallPath);
                    return Directory.Exists(pkg.InstallPath);
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
