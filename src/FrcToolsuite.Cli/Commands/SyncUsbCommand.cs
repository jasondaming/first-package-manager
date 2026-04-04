using FrcToolsuite.Cli.Output;
using FrcToolsuite.Core.Offline;
using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Cli.Commands;

public static class SyncUsbCommand
{
    public static async Task<int> ExecuteAsync(
        IOfflineCacheManager cacheManager,
        IPackageManager packageManager,
        IRegistryClient registryClient,
        string drivePath,
        string? bundleName,
        string? platformFilter = null)
    {
        try
        {
            if (!Directory.Exists(drivePath))
            {
                ConsoleHelper.WriteError($"Drive path '{drivePath}' does not exist.");
                return 1;
            }

            IReadOnlyList<string>? packageIds = null;

            if (!string.IsNullOrEmpty(bundleName))
            {
                // Resolve bundle to its package list
                ConsoleHelper.WriteInfo($"Resolving bundle '{bundleName}'...");
                var bundle = await registryClient.GetBundleAsync(bundleName);

                packageIds = bundle.Packages
                    .Where(p => p.Inclusion != PackageInclusion.Optional)
                    .Select(p => p.Id)
                    .ToList();

                ConsoleHelper.WriteInfo($"Bundle contains {packageIds.Count} packages.");
            }
            else
            {
                // Sync all installed packages
                var installed = await packageManager.GetInstalledPackagesAsync();
                if (installed.Count == 0)
                {
                    ConsoleHelper.WriteWarning("No packages installed. Nothing to sync.");
                    return 0;
                }

                packageIds = installed.Select(p => p.PackageId).ToList();
                ConsoleHelper.WriteInfo($"Syncing {packageIds.Count} installed packages to USB...");
            }

            // Show what will be synced
            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteInfo("Packages to sync:");
            foreach (var id in packageIds)
            {
                ConsoleHelper.WriteInfo($"  - {id}");
            }
            ConsoleHelper.WriteInfo("");

            // Ask for confirmation
            Console.Write($"Sync to '{drivePath}'? [y/N] ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (response != "y" && response != "yes")
            {
                ConsoleHelper.WriteInfo("Sync cancelled.");
                return 0;
            }

            // Execute with progress
            var progress = new Progress<OfflineSyncProgress>(p =>
            {
                ConsoleHelper.WriteProgressBar(
                    $"[{p.CompletedItems}/{p.TotalItems}] {p.CurrentItem}",
                    p.BytesTransferred,
                    p.TotalBytes);
            });

            // Parse platform filter (comma-separated: "windows-x64,macos-arm64")
            IReadOnlyList<string>? targetPlatforms = null;
            if (!string.IsNullOrEmpty(platformFilter))
            {
                targetPlatforms = platformFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                ConsoleHelper.WriteInfo($"Target platforms: {string.Join(", ", targetPlatforms)}");
            }
            else
            {
                ConsoleHelper.WriteInfo("Downloading all platforms (use --platform to filter)");
            }

            await cacheManager.ExportToUsbAsync(drivePath, packageIds, targetPlatforms, progress);

            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteSuccess($"USB sync complete. Packages exported to '{drivePath}'.");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"USB sync failed: {ex.Message}");
            return 1;
        }
    }
}
