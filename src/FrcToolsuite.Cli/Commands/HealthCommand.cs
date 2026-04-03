using FrcToolsuite.Cli.Output;
using FrcToolsuite.Core.Health;

namespace FrcToolsuite.Cli.Commands;

public static class HealthCommand
{
    public static async Task<int> ExecuteAsync(
        IHealthChecker healthChecker,
        bool fix,
        string? packageId)
    {
        try
        {
            ConsoleHelper.WriteInfo("Running health check...");
            ConsoleHelper.WriteInfo("");

            HealthReport report;
            if (!string.IsNullOrEmpty(packageId))
            {
                report = await healthChecker.RunCheckAsync(packageId);
            }
            else
            {
                report = await healthChecker.RunFullCheckAsync();
            }

            if (report.IsHealthy)
            {
                ConsoleHelper.WriteSuccess("All checks passed. Installation is healthy.");
                return 0;
            }

            var headers = new[] { "Severity", "Package", "Issue", "Auto-fixable" };
            var rows = report.Issues.Select(i => new[]
            {
                i.Severity.ToString(),
                i.PackageId ?? "(general)",
                i.Description,
                i.CanAutoFix ? "Yes" : "No"
            }).ToList();

            ConsoleHelper.WriteTable(headers, rows);
            ConsoleHelper.WriteInfo("");

            if (fix)
            {
                var fixableIssues = report.Issues.Where(i => i.CanAutoFix).ToList();
                if (fixableIssues.Count == 0)
                {
                    ConsoleHelper.WriteWarning("No issues can be automatically fixed.");
                    return 1;
                }

                ConsoleHelper.WriteInfo($"Attempting to fix {fixableIssues.Count} issue(s)...");
                int fixed_ = 0;
                foreach (var issue in fixableIssues)
                {
                    var success = await healthChecker.RepairAsync(issue);
                    if (success)
                    {
                        ConsoleHelper.WriteSuccess($"  Fixed: {issue.Description}");
                        fixed_++;
                    }
                    else
                    {
                        ConsoleHelper.WriteError($"  Failed to fix: {issue.Description}");
                    }
                }

                ConsoleHelper.WriteInfo($"{fixed_}/{fixableIssues.Count} issue(s) repaired.");
                return fixed_ == fixableIssues.Count ? 0 : 1;
            }

            ConsoleHelper.WriteWarning($"Found {report.Issues.Count} issue(s). Run with --fix to attempt auto-repair.");
            return 1;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Health check failed: {ex.Message}");
            return 1;
        }
    }
}
