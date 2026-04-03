using System.Text.Json;
using FrcToolsuite.Cli.Output;
using FrcToolsuite.Core.Configuration;

namespace FrcToolsuite.Cli.Commands;

public static class ConfigCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<int> ExecuteSetAsync(ISettingsProvider settingsProvider, string key, string value)
    {
        try
        {
            var settings = await settingsProvider.LoadAsync();
            if (!TrySetProperty(settings, key, value))
            {
                ConsoleHelper.WriteError($"Unknown configuration key: '{key}'");
                ConsoleHelper.WriteInfo("Run 'config list' to see available keys.");
                return 1;
            }

            await settingsProvider.SaveAsync(settings);
            ConsoleHelper.WriteSuccess($"Set '{key}' = '{value}'");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Failed to set config: {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> ExecuteGetAsync(ISettingsProvider settingsProvider, string key)
    {
        try
        {
            var settings = await settingsProvider.LoadAsync();
            var value = TryGetProperty(settings, key);
            if (value == null)
            {
                ConsoleHelper.WriteError($"Unknown configuration key: '{key}'");
                ConsoleHelper.WriteInfo("Run 'config list' to see available keys.");
                return 1;
            }

            ConsoleHelper.WriteInfo($"{key} = {value}");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Failed to get config: {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> ExecuteListAsync(ISettingsProvider settingsProvider)
    {
        try
        {
            var settings = await settingsProvider.LoadAsync();

            ConsoleHelper.WriteInfo("Current configuration:");
            ConsoleHelper.WriteInfo("");

            var headers = new[] { "Key", "Value" };
            var rows = new List<string[]>
            {
                new[] { "installDirectory", settings.InstallDirectory },
                new[] { "cacheDirectory", settings.CacheDirectory },
                new[] { "proxyUrl", settings.ProxyUrl ?? "(not set)" },
                new[] { "autoCheckUpdates", settings.AutoCheckUpdates.ToString() },
                new[] { "theme", settings.Theme },
                new[] { "lastRegistryFetch", settings.LastRegistryFetch?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(never)" },
                new[] { "selectedProgram", settings.SelectedProgram.ToString() },
                new[] { "selectedSeason", settings.SelectedSeason.ToString() },
            };

            ConsoleHelper.WriteTable(headers, rows);
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Failed to list config: {ex.Message}");
            return 1;
        }
    }

    private static bool TrySetProperty(AppSettings settings, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "installdirectory":
                settings.InstallDirectory = value;
                return true;
            case "cachedirectory":
                settings.CacheDirectory = value;
                return true;
            case "proxyurl":
                settings.ProxyUrl = string.IsNullOrEmpty(value) ? null : value;
                return true;
            case "autocheckupdates":
                if (bool.TryParse(value, out var b))
                {
                    settings.AutoCheckUpdates = b;
                    return true;
                }
                return false;
            case "theme":
                settings.Theme = value;
                return true;
            case "selectedprogram":
                if (Enum.TryParse<FrcToolsuite.Core.Registry.CompetitionProgram>(value, true, out var prog))
                {
                    settings.SelectedProgram = prog;
                    return true;
                }
                return false;
            case "selectedseason":
                if (int.TryParse(value, out var year))
                {
                    settings.SelectedSeason = year;
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static string? TryGetProperty(AppSettings settings, string key)
    {
        return key.ToLowerInvariant() switch
        {
            "installdirectory" => settings.InstallDirectory,
            "cachedirectory" => settings.CacheDirectory,
            "proxyurl" => settings.ProxyUrl ?? "(not set)",
            "autocheckupdates" => settings.AutoCheckUpdates.ToString(),
            "theme" => settings.Theme,
            "lastregistryfetch" => settings.LastRegistryFetch?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(never)",
            "selectedprogram" => settings.SelectedProgram.ToString(),
            "selectedseason" => settings.SelectedSeason.ToString(),
            _ => null,
        };
    }
}
