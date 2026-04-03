using FrcToolsuite.Cli.Output;
using FrcToolsuite.Core.Download;
using FrcToolsuite.Core.Update;

namespace FrcToolsuite.Cli.Commands;

public static class SelfUpdateCommand
{
    public static async Task<int> ExecuteAsync(ISelfUpdater updater)
    {
        ConsoleHelper.WriteInfo($"FIRST Package Manager v{updater.CurrentVersion}");
        ConsoleHelper.WriteInfo("Checking for updates...");

        var update = await updater.CheckForUpdateAsync().ConfigureAwait(false);

        if (update is null)
        {
            ConsoleHelper.WriteSuccess("You are running the latest version.");
            return 0;
        }

        ConsoleHelper.WriteInfo($"New version available: v{update.Version}");
        ConsoleHelper.WriteInfo($"  Download size: {ConsoleHelper.FormatSize(update.Size)}");

        if (!string.IsNullOrEmpty(update.ReleaseNotes))
        {
            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteInfo("Release notes:");
            ConsoleHelper.WriteInfo(update.ReleaseNotes);
            ConsoleHelper.WriteInfo("");
        }

        Console.Write("Download and install update? [Y/n] ");
        var response = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(response) &&
            !response.Equals("y", StringComparison.OrdinalIgnoreCase) &&
            !response.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            ConsoleHelper.WriteInfo("Update cancelled.");
            return 0;
        }

        ConsoleHelper.WriteInfo("Downloading update...");

        var progress = new Progress<DownloadProgress>(p =>
        {
            ConsoleHelper.WriteProgressBar("Downloading", p.BytesDownloaded, p.TotalBytes);
        });

        try
        {
            await updater.DownloadAndApplyUpdateAsync(update, progress).ConfigureAwait(false);
            ConsoleHelper.WriteSuccess("Update downloaded. Restarting to apply...");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Update failed: {ex.Message}");
            return 1;
        }
    }
}
