using FrcToolsuite.Core.Configuration;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Tests;

public class SettingsProviderTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "frctoolsuite-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_NoFile_ReturnsSensibleDefaults()
    {
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        var provider = new SettingsProvider(settingsPath);

        var settings = await provider.LoadAsync();

        Assert.False(string.IsNullOrEmpty(settings.InstallDirectory));
        Assert.True(settings.AutoCheckUpdates);
        Assert.Equal("system", settings.Theme);
        Assert.Equal(CompetitionProgram.Frc, settings.SelectedProgram);
        Assert.Equal(DateTime.Now.Year, settings.SelectedSeason);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTrips()
    {
        var settingsPath = Path.Combine(_tempDir, "sub", "settings.json");
        var provider = new SettingsProvider(settingsPath);

        var original = new AppSettings
        {
            InstallDirectory = "/custom/path",
            CacheDirectory = "/custom/cache",
            AutoCheckUpdates = false,
            Theme = "dark",
            SelectedProgram = CompetitionProgram.Ftc,
            SelectedSeason = 2025,
            ProxyUrl = "http://proxy.example.com:8080",
        };

        await provider.SaveAsync(original);
        var loaded = await provider.LoadAsync();

        Assert.Equal(original.InstallDirectory, loaded.InstallDirectory);
        Assert.Equal(original.CacheDirectory, loaded.CacheDirectory);
        Assert.Equal(original.AutoCheckUpdates, loaded.AutoCheckUpdates);
        Assert.Equal(original.Theme, loaded.Theme);
        Assert.Equal(original.SelectedProgram, loaded.SelectedProgram);
        Assert.Equal(original.SelectedSeason, loaded.SelectedSeason);
        Assert.Equal(original.ProxyUrl, loaded.ProxyUrl);
    }

    [Fact]
    public async Task ConcurrentAccess_DoesNotCorrupt()
    {
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        var provider = new SettingsProvider(settingsPath);

        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            var season = 2020 + i;
            tasks.Add(Task.Run(async () =>
            {
                var s = new AppSettings
                {
                    InstallDirectory = "/test",
                    SelectedSeason = season,
                };
                await provider.SaveAsync(s);
                await provider.LoadAsync();
            }));
        }

        await Task.WhenAll(tasks);

        // Verify we can still load valid settings after concurrent access
        var final = await provider.LoadAsync();
        Assert.False(string.IsNullOrEmpty(final.InstallDirectory));
    }
}
