using FrcToolsuite.Cli.Output;
using FrcToolsuite.Core.Install;

namespace FrcToolsuite.Cli.Commands;

/// <summary>
/// CLI command to uninstall previous WPILib year installations.
/// </summary>
public static class UninstallYearCommand
{
    /// <summary>
    /// Uninstall a specific year or all previous years.
    /// </summary>
    /// <param name="year">The specific year to uninstall, or null if using --all-previous.</param>
    /// <param name="allPrevious">If true, uninstall all years except the current year.</param>
    /// <param name="autoConfirm">If true, skip the confirmation prompt.</param>
    /// <param name="currentYear">The current season year (defaults to 2026).</param>
    /// <returns>Exit code: 0 for success, 1 for failure.</returns>
    public static async Task<int> ExecuteAsync(
        int? year,
        bool allPrevious,
        bool autoConfirm,
        int currentYear = 2026)
    {
        try
        {
            if (!allPrevious && year == null)
            {
                ConsoleHelper.WriteError("Please specify a year or use --all-previous.");
                return 1;
            }

            var previousYears = LegacyYearDetector.DetectPreviousYears(currentYear);

            if (previousYears.Count == 0)
            {
                ConsoleHelper.WriteInfo("No previous WPILib year installations found.");
                return 0;
            }

            List<LegacyYearDetector.YearInstall> toRemove;

            if (allPrevious)
            {
                toRemove = previousYears;
            }
            else
            {
                var match = previousYears.Find(y => y.Year == year);
                if (match == null)
                {
                    ConsoleHelper.WriteError($"WPILib {year} installation not found.");
                    ConsoleHelper.WriteInfo("");
                    ConsoleHelper.WriteInfo("Detected installations:");
                    foreach (var y in previousYears)
                    {
                        ConsoleHelper.WriteInfo($"  {y.Year}  ({y.SizeDisplay})  {y.Path}");
                    }

                    return 1;
                }

                toRemove = new List<LegacyYearDetector.YearInstall> { match };
            }

            // Display what will be removed
            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteInfo("The following WPILib installations will be removed:");
            var headers = new[] { "Year", "Size", "Path" };
            var rows = toRemove.Select(y => new[]
            {
                y.Year.ToString(),
                y.SizeDisplay,
                y.Path
            }).ToList();
            ConsoleHelper.WriteTable(headers, rows);

            long totalSize = toRemove.Sum(y => y.SizeBytes);
            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteInfo($"Total space to free: {ConsoleHelper.FormatSize(totalSize)}");

            // Confirmation
            if (!autoConfirm)
            {
                ConsoleHelper.WriteInfo("");
                Console.Write("Proceed with removal? [y/N] ");
                var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (response != "y" && response != "yes")
                {
                    ConsoleHelper.WriteInfo("Removal cancelled.");
                    return 0;
                }
            }

            // Execute removals
            foreach (var install in toRemove)
            {
                ConsoleHelper.WriteInfo("");
                ConsoleHelper.WriteInfo($"Removing WPILib {install.Year}...");

                var progress = new Progress<string>(message =>
                {
                    ConsoleHelper.WriteInfo($"  {message}");
                });

                await LegacyYearDetector.UninstallYearAsync(install.Year, progress);
            }

            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteSuccess(toRemove.Count == 1
                ? $"Successfully removed WPILib {toRemove[0].Year}."
                : $"Successfully removed {toRemove.Count} previous WPILib installations.");

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Removal failed: {ex.Message}");
            return 1;
        }
    }
}
