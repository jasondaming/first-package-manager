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
                ConsoleHelper.WriteInfo("");
                ConsoleHelper.WriteWarning("\u26A0 The following packages require administrator access:");
                foreach (var adminStep in plan.Steps.Where(s => s.RequiresAdmin))
                {
                    ConsoleHelper.WriteWarning($"  - {adminStep.PackageId}");
                }
                ConsoleHelper.WriteInfo("");
                ConsoleHelper.WriteInfo("Administrator privileges will be requested after non-admin packages install.");
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
            IReadOnlyList<string>? skippedPackages = null;
            var progress = new Progress<InstallProgress>(p =>
            {
                if (p.SkippedPackages != null && p.SkippedPackages.Count > 0)
                {
                    skippedPackages = p.SkippedPackages;
                    return;
                }

                var phase = p.Phase switch
                {
                    InstallPhase.Downloading => "Downloading",
                    InstallPhase.Extracting => "Extracting",
                    InstallPhase.Configuring => "Configuring",
                    InstallPhase.AwaitingAdmin => "Requesting admin",
                    _ => "Processing"
                };

                if (p.Phase == InstallPhase.AwaitingAdmin)
                {
                    ConsoleHelper.WriteInfo("");
                    ConsoleHelper.WriteWarning($"Requesting administrator privileges for: {p.CurrentPackageId}");
                }
                else
                {
                    ConsoleHelper.WriteProgressBar(
                        $"[{p.CurrentStep}/{p.TotalSteps}] {phase} {p.CurrentPackageId}",
                        p.BytesDownloaded,
                        p.TotalBytes);
                }
            });

            await packageManager.ExecutePlanAsync(plan, progress);

            ConsoleHelper.WriteInfo("");

            if (skippedPackages != null && skippedPackages.Count > 0)
            {
                ConsoleHelper.WriteWarning("The following packages were skipped (admin elevation denied):");
                foreach (var skipped in skippedPackages)
                {
                    ConsoleHelper.WriteWarning($"  - {skipped}");
                }
                ConsoleHelper.WriteInfo("");
            }

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
