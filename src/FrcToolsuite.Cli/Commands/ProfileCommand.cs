using System.Text.Json;
using FrcToolsuite.Cli.Output;
using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Cli.Commands;

public static class ProfileCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<int> ExecuteExportAsync(
        IPackageManager packageManager,
        string? outputPath)
    {
        try
        {
            var installed = await packageManager.GetInstalledPackagesAsync();

            if (installed.Count == 0)
            {
                ConsoleHelper.WriteWarning("No packages installed. Nothing to export.");
                return 1;
            }

            var profile = new TeamProfile
            {
                SchemaVersion = "1.0",
                ProfileName = "team-profile",
                Competition = CompetitionProgram.Frc,
                Season = installed.Max(p => p.Season),
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = Environment.UserName,
                Packages = installed.Select(p => p.PackageId).ToList(),
            };

            var filePath = outputPath ?? "team-profile.json";
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            ConsoleHelper.WriteSuccess($"Profile exported to '{filePath}'.");
            ConsoleHelper.WriteInfo($"  Packages: {profile.Packages.Count}");
            ConsoleHelper.WriteInfo($"  Season: {profile.Season}");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Export failed: {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> ExecuteImportAsync(string? inputPath)
    {
        try
        {
            var filePath = inputPath ?? "team-profile.json";

            if (!File.Exists(filePath))
            {
                ConsoleHelper.WriteError($"Profile file not found: '{filePath}'");
                return 1;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var profile = JsonSerializer.Deserialize<TeamProfile>(json, JsonOptions);

            if (profile == null)
            {
                ConsoleHelper.WriteError("Failed to parse profile file.");
                return 1;
            }

            ConsoleHelper.WriteInfo($"Profile: {profile.ProfileName}");
            if (profile.TeamNumber > 0)
            {
                ConsoleHelper.WriteInfo($"  Team: {profile.TeamNumber}");
            }
            ConsoleHelper.WriteInfo($"  Competition: {profile.Competition}");
            ConsoleHelper.WriteInfo($"  Season: {profile.Season}");
            ConsoleHelper.WriteInfo($"  Created: {profile.CreatedAt.LocalDateTime:yyyy-MM-dd HH:mm}");
            if (!string.IsNullOrEmpty(profile.CreatedBy))
            {
                ConsoleHelper.WriteInfo($"  Created by: {profile.CreatedBy}");
            }
            if (!string.IsNullOrEmpty(profile.Notes))
            {
                ConsoleHelper.WriteInfo($"  Notes: {profile.Notes}");
            }

            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteInfo($"Packages ({profile.Packages.Count}):");
            foreach (var pkg in profile.Packages)
            {
                ConsoleHelper.WriteInfo($"  - {pkg}");
            }

            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteInfo("Use 'frc profile apply' to install these packages.");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Import failed: {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> ExecuteApplyAsync(
        IPackageManager packageManager,
        string? inputPath)
    {
        try
        {
            var filePath = inputPath ?? "team-profile.json";

            if (!File.Exists(filePath))
            {
                ConsoleHelper.WriteError($"Profile file not found: '{filePath}'");
                return 1;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var profile = JsonSerializer.Deserialize<TeamProfile>(json, JsonOptions);

            if (profile == null)
            {
                ConsoleHelper.WriteError("Failed to parse profile file.");
                return 1;
            }

            if (profile.Packages.Count == 0)
            {
                ConsoleHelper.WriteWarning("Profile contains no packages.");
                return 0;
            }

            ConsoleHelper.WriteInfo($"Applying profile '{profile.ProfileName}'...");
            ConsoleHelper.WriteInfo($"Planning installation of {profile.Packages.Count} packages...");

            var plan = await packageManager.PlanInstallAsync(profile.Packages);

            if (plan.Steps.Count == 0)
            {
                ConsoleHelper.WriteSuccess("All packages in the profile are already installed.");
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
            Console.Write("Proceed with installation? [y/N] ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (response != "y" && response != "yes")
            {
                ConsoleHelper.WriteInfo("Apply cancelled.");
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
            ConsoleHelper.WriteSuccess("Profile applied successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Apply failed: {ex.Message}");
            return 1;
        }
    }
}
