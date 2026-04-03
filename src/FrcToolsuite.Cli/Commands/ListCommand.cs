using FrcToolsuite.Cli.Output;
using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Cli.Commands;

public static class ListCommand
{
    public static async Task<int> ExecuteAsync(
        IPackageManager packageManager,
        IRegistryClient registryClient,
        bool installed,
        bool available,
        bool updates)
    {
        try
        {
            if (available)
            {
                return await ListAvailableAsync(registryClient);
            }

            if (updates)
            {
                return await ListUpdatesAsync(packageManager);
            }

            // Default: show installed
            return await ListInstalledAsync(packageManager);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"List failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ListInstalledAsync(IPackageManager packageManager)
    {
        var packages = await packageManager.GetInstalledPackagesAsync();

        if (packages.Count == 0)
        {
            ConsoleHelper.WriteInfo("No packages installed.");
            return 0;
        }

        var headers = new[] { "Package", "Version", "Season", "Installed" };
        var rows = packages.Select(p => new[]
        {
            p.PackageId,
            p.Version,
            p.Season.ToString(),
            p.InstalledAt.LocalDateTime.ToString("yyyy-MM-dd")
        }).ToList();

        ConsoleHelper.WriteInfo($"Installed packages ({packages.Count}):");
        ConsoleHelper.WriteInfo("");
        ConsoleHelper.WriteTable(headers, rows);
        return 0;
    }

    private static async Task<int> ListAvailableAsync(IRegistryClient registryClient)
    {
        var index = await registryClient.FetchRegistryAsync();

        if (index.Packages.Count == 0)
        {
            ConsoleHelper.WriteInfo("No packages available in registry.");
            return 0;
        }

        var headers = new[] { "Package", "Name", "Version", "Category", "Season" };
        var rows = index.Packages.Select(p => new[]
        {
            p.Id,
            p.Name,
            p.Version,
            p.Category,
            p.Season.ToString()
        }).ToList();

        ConsoleHelper.WriteInfo($"Available packages ({index.Packages.Count}):");
        ConsoleHelper.WriteInfo("");
        ConsoleHelper.WriteTable(headers, rows);

        if (registryClient.IsOffline)
        {
            ConsoleHelper.WriteWarning("Note: Showing cached data (offline mode).");
        }

        return 0;
    }

    private static async Task<int> ListUpdatesAsync(IPackageManager packageManager)
    {
        var updates = await packageManager.CheckForUpdatesAsync();

        if (updates.Count == 0)
        {
            ConsoleHelper.WriteSuccess("All packages are up to date.");
            return 0;
        }

        var headers = new[] { "Package", "Installed", "Available" };
        var rows = updates.Select(u => new[]
        {
            u.PackageId,
            u.InstalledVersion,
            u.AvailableVersion
        }).ToList();

        ConsoleHelper.WriteInfo($"Updates available ({updates.Count}):");
        ConsoleHelper.WriteInfo("");
        ConsoleHelper.WriteTable(headers, rows);
        return 0;
    }
}
