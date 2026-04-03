using System.IO.Compression;
using System.Text.Json;
using FrcToolsuite.Core.Configuration;
using FrcToolsuite.Core.Download;
using FrcToolsuite.Core.Install;
using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Platform;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Tests;

public class IntegrationTests : IDisposable
{
    private readonly string _installRoot;
    private readonly string _cacheDir;

    public IntegrationTests()
    {
        _installRoot = Path.Combine(Path.GetTempPath(), "frc-integration-test", Guid.NewGuid().ToString());
        _cacheDir = Path.Combine(_installRoot, ".cache");
        Directory.CreateDirectory(_installRoot);
        Directory.CreateDirectory(_cacheDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_installRoot))
        {
            Directory.Delete(_installRoot, recursive: true);
        }
    }

    [Fact]
    public async Task FullInstallAndUninstall_EndToEnd()
    {
        // 1. Arrange: Create test package manifests
        var testManifest = new PackageManifest
        {
            Id = "test.tool",
            Name = "Test Tool",
            Version = "1.0.0",
            Season = 2026,
            Publisher = "test-publisher",
            Description = "A test tool for integration testing.",
            Category = "tool",
            Competition = CompetitionProgram.Frc,
            Dependencies = [],
            Artifacts = new Dictionary<string, PackageArtifact>
            {
                ["windows-x64"] = new PackageArtifact
                {
                    Url = "https://example.com/test-tool-1.0.0.zip",
                    Sha256 = "0000000000000000000000000000000000000000000000000000000000000000",
                    Size = 1024,
                    ArchiveType = "zip",
                    Filename = "test-tool-1.0.0.zip",
                }
            }
        };

        var manifests = new Dictionary<string, PackageManifest>
        {
            ["test.tool"] = testManifest
        };

        // 2. Set up fake registry client
        var registry = new IntegrationFakeRegistryClient(manifests);

        // 3. Create a fake download manager that writes a real zip archive
        var downloadManager = new ZipCreatingDownloadManager(_cacheDir);

        // 4. Wire up real InstallEngine with stub platform service
        var platformService = new IntegrationStubPlatformService();
        var installEngine = new InstallEngine(platformService);
        var settingsProvider = new IntegrationSettingsProvider(_installRoot, _cacheDir);

        var packageManager = new PackageManager(
            registry, downloadManager, installEngine, platformService, settingsProvider);

        // 5. Plan install
        var installPlan = await packageManager.PlanInstallAsync(new[] { "test.tool" });

        Assert.Single(installPlan.Steps);
        Assert.Equal("test.tool", installPlan.Steps[0].PackageId);
        Assert.Equal(InstallAction.Install, installPlan.Steps[0].Action);

        // 6. Execute the install plan
        await packageManager.ExecutePlanAsync(installPlan);

        // 7. Verify: files extracted, install manifest created, package is in installed list
        var pkgInstallDir = Path.Combine(_installRoot, "test.tool");
        Assert.True(Directory.Exists(pkgInstallDir), "Package install directory should exist.");

        var manifestPath = Path.Combine(pkgInstallDir, ".install-manifest.json");
        Assert.True(File.Exists(manifestPath), "Install manifest should exist.");

        var testFilePath = Path.Combine(pkgInstallDir, "test-content.txt");
        Assert.True(File.Exists(testFilePath), "Extracted test file should exist.");

        var installedContent = await File.ReadAllTextAsync(testFilePath);
        Assert.StartsWith("Hello from ", installedContent);
        Assert.EndsWith(" integration test!", installedContent);

        var installed = await packageManager.GetInstalledPackagesAsync();
        Assert.Single(installed);
        Assert.Equal("test.tool", installed[0].PackageId);
        Assert.Equal("1.0.0", installed[0].Version);
        Assert.Equal(2026, installed[0].Season);

        // 8. Plan uninstall
        var uninstallPlan = await packageManager.PlanUninstallAsync(new[] { "test.tool" });

        Assert.Single(uninstallPlan.Steps);
        Assert.Equal("test.tool", uninstallPlan.Steps[0].PackageId);
        Assert.Equal(InstallAction.Uninstall, uninstallPlan.Steps[0].Action);

        // 9. Execute uninstall
        await packageManager.ExecutePlanAsync(uninstallPlan);

        // 10. Verify: files removed, package no longer in installed list
        Assert.False(File.Exists(testFilePath), "Extracted file should be removed after uninstall.");
        Assert.False(File.Exists(manifestPath), "Install manifest should be removed after uninstall.");

        var installedAfter = await packageManager.GetInstalledPackagesAsync();
        Assert.Empty(installedAfter);
    }

    [Fact]
    public async Task InstallWithDependency_ResolvesAndInstallsBoth()
    {
        var baseManifest = new PackageManifest
        {
            Id = "base.runtime",
            Name = "Base Runtime",
            Version = "2.0.0",
            Season = 2026,
            Publisher = "test-publisher",
            Description = "Base runtime dependency.",
            Category = "runtime",
            Competition = CompetitionProgram.Frc,
            Dependencies = [],
            Artifacts = new Dictionary<string, PackageArtifact>
            {
                ["windows-x64"] = new PackageArtifact
                {
                    Url = "https://example.com/base-runtime-2.0.0.zip",
                    Sha256 = "0000000000000000000000000000000000000000000000000000000000000000",
                    Size = 2048,
                    ArchiveType = "zip",
                    Filename = "base-runtime-2.0.0.zip",
                }
            }
        };

        var appManifest = new PackageManifest
        {
            Id = "test.app",
            Name = "Test App",
            Version = "1.0.0",
            Season = 2026,
            Publisher = "test-publisher",
            Description = "An app that depends on base runtime.",
            Category = "tool",
            Competition = CompetitionProgram.Frc,
            Dependencies =
            [
                new PackageDependency
                {
                    Id = "base.runtime",
                    Type = DependencyType.Required
                }
            ],
            Artifacts = new Dictionary<string, PackageArtifact>
            {
                ["windows-x64"] = new PackageArtifact
                {
                    Url = "https://example.com/test-app-1.0.0.zip",
                    Sha256 = "0000000000000000000000000000000000000000000000000000000000000000",
                    Size = 512,
                    ArchiveType = "zip",
                    Filename = "test-app-1.0.0.zip",
                }
            }
        };

        var manifests = new Dictionary<string, PackageManifest>
        {
            ["base.runtime"] = baseManifest,
            ["test.app"] = appManifest
        };

        var registry = new IntegrationFakeRegistryClient(manifests);
        var downloadManager = new ZipCreatingDownloadManager(_cacheDir);
        var platformService = new IntegrationStubPlatformService();
        var installEngine = new InstallEngine(platformService);
        var settingsProvider = new IntegrationSettingsProvider(_installRoot, _cacheDir);

        var pm = new PackageManager(registry, downloadManager, installEngine, platformService, settingsProvider);

        // Request just test.app; dependency resolver should pull in base.runtime
        var plan = await pm.PlanInstallAsync(new[] { "test.app" });

        Assert.Equal(2, plan.Steps.Count);
        Assert.Equal("base.runtime", plan.Steps[0].PackageId);
        Assert.Equal("test.app", plan.Steps[1].PackageId);

        await pm.ExecutePlanAsync(plan);

        var installed = await pm.GetInstalledPackagesAsync();
        Assert.Equal(2, installed.Count);
        Assert.Contains(installed, p => p.PackageId == "base.runtime");
        Assert.Contains(installed, p => p.PackageId == "test.app");

        // Verify both have extracted files
        Assert.True(File.Exists(Path.Combine(_installRoot, "base.runtime", "test-content.txt")));
        Assert.True(File.Exists(Path.Combine(_installRoot, "test.app", "test-content.txt")));
    }
}

/// <summary>
/// A download manager that creates real zip files containing a test file,
/// enabling end-to-end extraction testing with the real InstallEngine.
/// </summary>
internal sealed class ZipCreatingDownloadManager : IDownloadManager
{
    private readonly string _cacheDir;

    public ZipCreatingDownloadManager(string cacheDir)
    {
        _cacheDir = cacheDir;
    }

    public Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(request.TargetPath)!);

        // Infer a package name from the URL for the test file content
        var fileName = Path.GetFileNameWithoutExtension(request.Url);

        // Create a real zip archive with a test file inside
        using (var zipStream = new FileStream(request.TargetPath, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("test-content.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write($"Hello from {fileName} integration test!");
        }

        return Task.FromResult(new DownloadResult(true, request.TargetPath));
    }

    public Task<IReadOnlyList<DownloadResult>> DownloadParallelAsync(
        IReadOnlyList<DownloadRequest> requests,
        int maxConcurrency = 4,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<DownloadResult>();
        foreach (var request in requests)
        {
            results.Add(DownloadAsync(request, progress, ct).Result);
        }
        return Task.FromResult<IReadOnlyList<DownloadResult>>(results);
    }
}

internal sealed class IntegrationFakeRegistryClient : IRegistryClient
{
    private readonly Dictionary<string, PackageManifest> _manifests;
    private readonly RegistryIndex _index;

    public IntegrationFakeRegistryClient(Dictionary<string, PackageManifest> manifests)
    {
        _manifests = manifests;
        _index = new RegistryIndex
        {
            SchemaVersion = "1.0",
            LastUpdated = DateTimeOffset.UtcNow,
            Packages = manifests.Values.Select(m => new PackageSummary
            {
                Id = m.Id,
                Name = m.Name,
                Version = m.Version,
                Season = m.Season,
                Category = m.Category,
                Competition = m.Competition,
                Description = m.Description,
                ManifestUrl = $"https://example.com/{m.Id}.json"
            }).ToList()
        };
    }

    public bool IsOffline => false;

    public Task<RegistryIndex> FetchRegistryAsync(bool forceRefresh = false, CancellationToken ct = default)
        => Task.FromResult(_index);

    public Task<PackageManifest> GetPackageAsync(string packageId, CancellationToken ct = default)
    {
        if (_manifests.TryGetValue(packageId, out var manifest))
        {
            return Task.FromResult(manifest);
        }
        throw new KeyNotFoundException($"Package '{packageId}' not found.");
    }

    public Task<IReadOnlyList<PackageSummary>> SearchAsync(
        string? query = null,
        CompetitionProgram? program = null,
        int? year = null,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PackageSummary>>(_index.Packages);

    public Task<BundleDefinition> GetBundleAsync(string bundleId, CancellationToken ct = default)
        => throw new KeyNotFoundException($"Bundle '{bundleId}' not found.");
}

internal sealed class IntegrationStubPlatformService : IPlatformService
{
    public bool IsAdminElevated => false;
    public void CreateShortcut(string name, string targetPath, string? iconPath = null, bool isDesktop = false) { }
    public void RemoveShortcut(string name, bool isDesktop = false) { }
    public void AddToPath(string path) { }
    public void RemoveFromPath(string path) { }
    public void SetEnvironmentVariable(string name, string value) { }
    public void RemoveEnvironmentVariable(string name) { }
    public string GetPlatformId() => "windows-x64";
    public Task RequestAdminElevationAsync(IReadOnlyList<string> operations) => Task.CompletedTask;
}

internal sealed class IntegrationSettingsProvider : ISettingsProvider
{
    private readonly string _installDir;
    private readonly string _cacheDir;

    public IntegrationSettingsProvider(string installDir, string cacheDir)
    {
        _installDir = installDir;
        _cacheDir = cacheDir;
    }

    public Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new AppSettings
        {
            InstallDirectory = _installDir,
            CacheDirectory = _cacheDir
        });
    }

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
}
