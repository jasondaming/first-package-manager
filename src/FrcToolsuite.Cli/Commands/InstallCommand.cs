using FrcToolsuite.Cli.Output;
using FrcToolsuite.Core.Packages;

namespace FrcToolsuite.Cli.Commands;

public static class InstallCommand
{
    public static async Task<int> ExecuteAsync(
        IPackageManager packageManager,
        string packageOrBundle,
        bool isBundle,
        bool autoConfirm)
    {
        try
        {
            ConsoleHelper.WriteInfo($"Planning installation for '{packageOrBundle}'...");

            InstallPlan plan;
            if (isBundle)
            {
                plan = await packageManager.PlanBundleInstallAsync(packageOrBundle);
            }
            else
            {
                plan = await packageManager.PlanInstallAsync(new[] { packageOrBundle });
            }

            if (plan.Steps.Count == 0)
            {
                ConsoleHelper.WriteInfo("Nothing to install.");
                return 0;
            }

            // Display the plan
            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteInfo("Install plan:");
            var headers = new[] { "Package", "Version", "Action", "Size" };
            var rows = plan.Steps.Select(s => new[]
            {
                s.PackageId,
                s.Version,
                s.Action.ToString(),
                ConsoleHelper.FormatSize(s.DownloadSize)
            }).ToList();
            ConsoleHelper.WriteTable(headers, rows);

            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteInfo($"Total download size: {ConsoleHelper.FormatSize(plan.TotalDownloadSize)}");

            if (plan.RequiresAdminElevation)
            {
                ConsoleHelper.WriteWarning("This installation requires administrator privileges.");
            }

            // Ask for confirmation
            if (!autoConfirm)
            {
                Console.Write("Proceed with installation? [y/N] ");
                var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (response != "y" && response != "yes")
                {
                    ConsoleHelper.WriteInfo("Installation cancelled.");
                    return 0;
                }
            }

            // Execute with progress
            var progress = new Progress<InstallProgress>(p =>
            {
                var phase = p.Phase switch
                {
                    InstallPhase.Downloading => "Downloading",
                    InstallPhase.Extracting => "Extracting",
                    InstallPhase.Configuring => "Configuring",
                    _ => "Processing"
                };
                ConsoleHelper.WriteProgressBar(
                    $"[{p.CurrentStep}/{p.TotalSteps}] {phase} {p.CurrentPackageId}",
                    p.BytesDownloaded,
                    p.TotalBytes);
            });

            await packageManager.ExecutePlanAsync(plan, progress);

            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteSuccess("Done.");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Install failed: {ex.Message}");
            return 1;
        }
    }
}
