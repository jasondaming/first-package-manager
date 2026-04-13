using System.Runtime.InteropServices;

namespace FrcToolsuite.Core.Install;

/// <summary>
/// Scans for previous WPILib year installations and provides uninstall capabilities.
/// WPILib installs to C:\Users\Public\wpilib\{year}\ on Windows or ~/wpilib/{year}/ on Mac/Linux.
/// </summary>
public static class LegacyYearDetector
{
    /// <summary>
    /// Represents a discovered WPILib year installation.
    /// </summary>
    /// <param name="Year">The season year (e.g. 2025).</param>
    /// <param name="Path">The full path to the year directory.</param>
    /// <param name="SizeBytes">Total size of the installation in bytes.</param>
    public record YearInstall(int Year, string Path, long SizeBytes)
    {
        /// <summary>
        /// Human-readable size display.
        /// </summary>
        public string SizeDisplay => SizeBytes switch
        {
            >= 1_000_000_000 => $"{SizeBytes / 1_000_000_000.0:F1} GB",
            >= 1_000_000 => $"{SizeBytes / 1_000_000.0:F0} MB",
            >= 1_000 => $"{SizeBytes / 1_000.0:F0} KB",
            _ => $"{SizeBytes} B"
        };
    }

    /// <summary>
    /// Returns all WPILib year directories found, excluding the current year.
    /// Scans year directories from 2019 through currentYear-1.
    /// </summary>
    /// <param name="currentYear">The current season year to exclude (defaults to 2026).</param>
    /// <returns>A list of discovered year installations sorted by year descending.</returns>
    public static List<YearInstall> DetectPreviousYears(int currentYear = 2026)
    {
        var results = new List<YearInstall>();
        var baseDirs = GetWpilibBaseDirs();

        for (int year = 2019; year < currentYear; year++)
        {
            foreach (var baseDir in baseDirs)
            {
                var yearPath = System.IO.Path.Combine(baseDir, year.ToString());
                if (Directory.Exists(yearPath))
                {
                    var size = CalculateDirectorySize(yearPath);
                    results.Add(new YearInstall(year, yearPath, size));
                    break; // Found this year, no need to check other base dirs
                }
            }
        }

        results.Sort((a, b) => b.Year.CompareTo(a.Year));
        return results;
    }

    /// <summary>
    /// Uninstall a year by deleting its directory and cleaning up related artifacts.
    /// </summary>
    /// <param name="year">The year to uninstall.</param>
    /// <param name="progress">Optional progress reporter for status updates.</param>
    /// <returns>A task that completes when the uninstall is finished.</returns>
    public static async Task UninstallYearAsync(int year, IProgress<string>? progress = null)
    {
        var baseDirs = GetWpilibBaseDirs();
        string? yearPath = null;

        foreach (var baseDir in baseDirs)
        {
            var candidate = System.IO.Path.Combine(baseDir, year.ToString());
            if (Directory.Exists(candidate))
            {
                yearPath = candidate;
                break;
            }
        }

        if (yearPath == null)
        {
            progress?.Report($"No installation found for {year}.");
            return;
        }

        // Report known subdirectories as we remove them
        await DeleteSubdirectoryAsync(yearPath, "jdk", "Removing JDK...", progress);
        await DeleteSubdirectoryAsync(yearPath, "vscode", "Removing VS Code...", progress);
        await DeleteSubdirectoryAsync(yearPath, "maven", "Removing Maven cache...", progress);
        await DeleteSubdirectoryAsync(yearPath, "tools", "Removing WPILib tools...", progress);
        await DeleteSubdirectoryAsync(yearPath, "advantagescope", "Removing AdvantageScope...", progress);
        await DeleteSubdirectoryAsync(yearPath, "elastic", "Removing Elastic...", progress);
        await DeleteSubdirectoryAsync(yearPath, "vendordeps", "Removing vendor dependencies...", progress);

        // Remove any remaining files and the directory itself
        progress?.Report("Removing remaining files...");
        var remainingErrors = new List<string>();
        await Task.Run(() =>
        {
            if (Directory.Exists(yearPath))
            {
                remainingErrors.AddRange(ForceDeleteDirectory(yearPath));
            }
        });

        // Clean up desktop shortcuts that reference this year
        progress?.Report("Cleaning up shortcuts...");
        await CleanupShortcutsAsync(year);

        // Clean up PATH entries for this year's JDK
        progress?.Report("Cleaning up PATH entries...");
        await CleanupPathEntriesAsync(year);

        if (remainingErrors.Count > 0)
        {
            progress?.Report($"Partially removed WPILib {year}. Some files were locked — close VS Code and retry.");
            throw new IOException(
                $"Some files in WPILib {year} could not be deleted (likely locked by VS Code or another process). Close all programs using the WPILib directory and try again.");
        }

        progress?.Report($"Successfully removed WPILib {year}.");
    }

    /// <summary>
    /// Forcefully delete a directory, clearing read-only attributes and retrying
    /// on transient file locks. Returns any files that could not be deleted.
    /// </summary>
    private static List<string> ForceDeleteDirectory(string path)
    {
        var errors = new List<string>();

        // First pass: clear read-only attributes on all files/directories
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var attrs = File.GetAttributes(file);
                    if ((attrs & FileAttributes.ReadOnly) != 0)
                    {
                        File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
                    }
                }
                catch { /* best effort */ }
            }
        }
        catch { /* best effort */ }

        // Retry delete up to 3 times with small delays (handles transient locks
        // from antivirus scans, Windows Search indexer, etc.)
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return errors; // Success
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 2)
                {
                    errors.Add(ex.Message);
                }
                else
                {
                    Thread.Sleep(500);
                }
            }
        }

        return errors;
    }

    private static List<string> GetWpilibBaseDirs()
    {
        var dirs = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Primary location: C:\Users\Public\wpilib
            dirs.Add(@"C:\Users\Public\wpilib");

            // Also try via environment variable
            var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var altPath = commonData.Replace("ProgramData", @"Users\Public");
            var altWpilib = System.IO.Path.Combine(altPath, "wpilib");
            if (!dirs.Contains(altWpilib, StringComparer.OrdinalIgnoreCase))
            {
                dirs.Add(altWpilib);
            }
        }

        // User home ~/wpilib (used on Mac/Linux, sometimes Windows too)
        var homePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "wpilib");
        if (!dirs.Contains(homePath, StringComparer.OrdinalIgnoreCase))
        {
            dirs.Add(homePath);
        }

        return dirs;
    }

    private static long CalculateDirectorySize(string path)
    {
        long size = 0;
        try
        {
            var dirInfo = new DirectoryInfo(path);
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    size += file.Length;
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip files we can't access
                }
                catch (IOException)
                {
                    // Skip files with IO errors
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Can't enumerate directory at all
        }

        return size;
    }

    private static async Task DeleteSubdirectoryAsync(
        string yearPath,
        string subdirectory,
        string statusMessage,
        IProgress<string>? progress)
    {
        var subPath = System.IO.Path.Combine(yearPath, subdirectory);
        if (Directory.Exists(subPath))
        {
            progress?.Report(statusMessage);
            await Task.Run(() => Directory.Delete(subPath, recursive: true));
        }
    }

    private static async Task CleanupShortcutsAsync(int year)
    {
        await Task.Run(() =>
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var publicDesktop = @"C:\Users\Public\Desktop";

            var desktopPaths = new[] { desktopPath, publicDesktop };

            foreach (var desktop in desktopPaths)
            {
                if (!Directory.Exists(desktop))
                {
                    continue;
                }

                try
                {
                    foreach (var shortcut in Directory.GetFiles(desktop, "*.lnk"))
                    {
                        try
                        {
                            // Check if the shortcut name references this year
                            var fileName = System.IO.Path.GetFileNameWithoutExtension(shortcut);
                            if (fileName.Contains(year.ToString(), StringComparison.OrdinalIgnoreCase))
                            {
                                File.Delete(shortcut);
                            }
                        }
                        catch (IOException)
                        {
                            // Best effort cleanup
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Skip shortcuts we can't delete
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Can't enumerate desktop
                }
            }
        });
    }

    private static async Task CleanupPathEntriesAsync(int year)
    {
        await Task.Run(() =>
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            try
            {
                var yearStr = year.ToString();
                var pathVar = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                if (string.IsNullOrEmpty(pathVar))
                {
                    return;
                }

                var entries = pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries);
                var filtered = entries.Where(e =>
                    !e.Contains($"wpilib\\{yearStr}", StringComparison.OrdinalIgnoreCase) &&
                    !e.Contains($"wpilib/{yearStr}", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (filtered.Length < entries.Length)
                {
                    var newPath = string.Join(';', filtered);
                    Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
                }
            }
            catch (System.Security.SecurityException)
            {
                // Can't modify PATH without appropriate permissions
            }
        });
    }
}
