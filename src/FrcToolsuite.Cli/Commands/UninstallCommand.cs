using FrcToolsuite.Cli.Output;
using FrcToolsuite.Core.Packages;

namespace FrcToolsuite.Cli.Commands;

public static class UninstallCommand
{
    public static async Task<int> ExecuteAsync(
        IPackageManager packageManager,
        string packageId)
    {
        try
        {
            ConsoleHelper.WriteInfo($"Planning uninstall for '{packageId}'...");

            InstallPlan plan;
            try
            {
                plan = await packageManager.PlanUninstallAsync(new[] { packageId });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("required by"))
            {
                ConsoleHelper.WriteError(ex.Message);
                ConsoleHelper.WriteInfo("Uninstall the dependent package first, or use --force (not yet supported).");
                return 1;
            }

            if (plan.Steps.Count == 0)
            {
                ConsoleHelper.WriteWarning($"Package '{packageId}' is not installed.");
                return 1;
            }

            // Display what will be removed
            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteInfo("The following packages will be removed:");
            var headers = new[] { "Package", "Version" };
            var rows = plan.Steps.Select(s => new[]
            {
                s.PackageId,
                s.Version
            }).ToList();
            ConsoleHelper.WriteTable(headers, rows);

            // Ask for confirmation
            ConsoleHelper.WriteInfo("");
            Console.Write("Proceed with uninstall? [y/N] ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (response != "y" && response != "yes")
            {
                ConsoleHelper.WriteInfo("Uninstall cancelled.");
                return 0;
            }

            // Execute with progress
            var progress = new Progress<InstallProgress>(p =>
            {
                ConsoleHelper.WriteProgressBar(
                    $"[{p.CurrentStep}/{p.TotalSteps}] Removing {p.CurrentPackageId}",
                    p.BytesDownloaded,
                    p.TotalBytes);
            });

            await packageManager.ExecutePlanAsync(plan, progress);

            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteSuccess($"Successfully uninstalled '{packageId}'.");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Uninstall failed: {ex.Message}");
            return 1;
        }
    }
}
