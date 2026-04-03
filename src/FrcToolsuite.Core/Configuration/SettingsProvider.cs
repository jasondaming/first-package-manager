using System.Text.Json;

namespace FrcToolsuite.Core.Configuration;

public class SettingsProvider : ISettingsProvider
{
    private static readonly string DefaultSettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".frctoolsuite");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _settingsFilePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public SettingsProvider()
        : this(Path.Combine(DefaultSettingsDir, "settings.json"))
    {
    }

    public SettingsProvider(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return CreateDefaults();
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath, ct).ConfigureAwait(false);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? CreateDefaults();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json, ct).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static AppSettings CreateDefaults()
    {
        var isWindows = OperatingSystem.IsWindows();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return new AppSettings
        {
            InstallDirectory = isWindows ? @"C:\frc" : Path.Combine(home, "frc"),
            CacheDirectory = Path.Combine(DefaultSettingsDir, "cache"),
            AutoCheckUpdates = true,
            Theme = "system",
            SelectedProgram = Registry.CompetitionProgram.Frc,
            SelectedSeason = DateTime.Now.Year,
        };
    }
}
