using FrcToolsuite.Cli.Output;
using FrcToolsuite.Core.Packages;

namespace FrcToolsuite.Cli.Commands;

public static class UpdateCommand
{
    public static async Task<int> ExecuteAsync(
        IPackageManager packageManager,
        string? packageId,
        bool all)
    {
        try
        {
            if (!all && string.IsNullOrEmpty(packageId))
            {
                // Interactive: show available updates and let user choose
                return await ExecuteInteractiveAsync(packageManager);
            }

            ConsoleHelper.WriteInfo("Checking for updates...");

            InstallPlan plan;
            if (all)
            {
                plan = await packageManager.PlanUpdateAsync();
            }
            else
            {
                plan = await packageManager.PlanUpdateAsync(new[] { packageId! });
            }

            if (plan.Steps.Count == 0)
            {
                ConsoleHelper.WriteSuccess("All packages are up to date.");
                return 0;
            }

            // Display the plan
            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteInfo("Update plan:");
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
                ConsoleHelper.WriteWarning("This update requires administrator privileges.");
            }

            // Ask for confirmation
            Console.Write("Proceed with update? [y/N] ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (response != "y" && response != "yes")
            {
                ConsoleHelper.WriteInfo("Update cancelled.");
                return 0;
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
            ConsoleHelper.WriteSuccess("Update complete.");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Update failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ExecuteInteractiveAsync(IPackageManager packageManager)
    {
        ConsoleHelper.WriteInfo("Checking for updates...");

        var updates = await packageManager.CheckForUpdatesAsync();

        if (updates.Count == 0)
        {
            ConsoleHelper.WriteSuccess("All packages are up to date.");
            return 0;
        }

        ConsoleHelper.WriteInfo("");
        ConsoleHelper.WriteInfo($"Updates available ({updates.Count}):");
        ConsoleHelper.WriteInfo("");

        var headers = new[] { "#", "Package", "Installed", "Available" };
        var rows = updates.Select((u, i) => new[]
        {
            (i + 1).ToString(),
            u.PackageId,
            u.InstalledVersion,
            u.AvailableVersion
        }).ToList();
        ConsoleHelper.WriteTable(headers, rows);

        ConsoleHelper.WriteInfo("");
        Console.Write("Enter package numbers to update (comma-separated), 'all', or 'q' to quit: ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(input) || input == "q")
        {
            ConsoleHelper.WriteInfo("Update cancelled.");
            return 0;
        }

        IReadOnlyList<string> selectedIds;
        if (input == "all")
        {
            selectedIds = updates.Select(u => u.PackageId).ToList();
        }
        else
        {
            var indices = new List<int>();
            foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var num) && num >= 1 && num <= updates.Count)
                {
                    indices.Add(num - 1);
                }
                else
                {
                    ConsoleHelper.WriteError($"Invalid selection: '{part}'");
                    return 1;
                }
            }
            selectedIds = indices.Select(i => updates[i].PackageId).ToList();
        }

        var plan = await packageManager.PlanUpdateAsync(selectedIds);

        if (plan.Steps.Count == 0)
        {
            ConsoleHelper.WriteSuccess("Nothing to update.");
            return 0;
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
        ConsoleHelper.WriteSuccess("Update complete.");
        return 0;
    }
}
