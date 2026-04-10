using FrcToolsuite.Core.Configuration;
using FrcToolsuite.Core.Download;
using FrcToolsuite.Core.Install;
using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Platform;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Tests;

public class PackageManagerTests
{
    private static readonly string TestInstallDir = Path.Combine(Path.GetTempPath(), "frc-test-install");

    [Fact]
    public async Task PlanInstallAsync_CreatesCorrectSteps()
    {
        var manifests = new Dictionary<string, PackageManifest>
        {
            ["wpilib"] = MakeManifest("wpilib", "2026.1.0", size: 5000),
            ["navx"] = MakeManifest("navx", "2026.1.0", size: 2000,
                deps: new[] { MakeDep("wpilib") })
        };

        var pm = CreatePackageManager(manifests);
        var plan = await pm.PlanInstallAsync(new[] { "navx" });

        Assert.Equal(2, plan.Steps.Count);
        Assert.Equal("wpilib", plan.Steps[0].PackageId);
        Assert.Equal("navx", plan.Steps[1].PackageId);
        Assert.Equal(InstallAction.Install, plan.Steps[0].Action);
        Assert.Equal(InstallAction.Install, plan.Steps[1].Action);
        Assert.Equal(7000, plan.TotalDownloadSize);
    }

    [Fact]
    public async Task PlanBundleInstallAsync_RespectsInclusionLevels()
    {
        var manifests = new Dictionary<string, PackageManifest>
        {
            ["wpilib"] = MakeManifest("wpilib", "2026.1.0", size: 5000),
            ["dashboard"] = MakeManifest("dashboard", "2026.1.0", size: 3000),
            ["extras"] = MakeManifest("extras", "1.0.0", size: 1000)
        };

        var bundles = new Dictionary<string, BundleDefinition>
        {
            ["frc-starter"] = new BundleDefinition
            {
                Id = "frc-starter",
                Name = "FRC Starter",
                Packages = new List<BundlePackageRef>
                {
                    new() { Id = "wpilib", Inclusion = PackageInclusion.Required },
                    new() { Id = "dashboard", Inclusion = PackageInclusion.Default },
                    new() { Id = "extras", Inclusion = PackageInclusion.Optional }
                }
            }
        };

        // Without optional packages
        var pm = CreatePackageManager(manifests, bundles: bundles);
        var plan = await pm.PlanBundleInstallAsync("frc-starter");

        Assert.Equal(2, plan.Steps.Count);
        Assert.Contains(plan.Steps, s => s.PackageId == "wpilib");
        Assert.Contains(plan.Steps, s => s.PackageId == "dashboard");
        Assert.DoesNotContain(plan.Steps, s => s.PackageId == "extras");

        // With optional packages included
        var plan2 = await pm.PlanBundleInstallAsync("frc-starter", new[] { "extras" });

        Assert.Equal(3, plan2.Steps.Count);
        Assert.Contains(plan2.Steps, s => s.PackageId == "extras");
    }

    [Fact]
    public async Task PlanUpdateAsync_DetectsOutdatedPackages()
    {
        var manifests = new Dictionary<string, PackageManifest>
        {
            ["wpilib"] = MakeManifest("wpilib", "2026.2.0", size: 5000),
            ["navx"] = MakeManifest("navx", "2026.1.0", size: 2000)
        };

        var index = new RegistryIndex
        {
            Packages = manifests.Values.Select(m => new PackageSummary
            {
                Id = m.Id,
                Name = m.Name,
                Version = m.Version,
                Season = m.Season
            }).ToList()
        };

        // Create install manifests on disk to simulate installed packages
        var installDir = Path.Combine(TestInstallDir, Guid.NewGuid().ToString());

        try
        {
            CreateInstalledManifest(installDir, "wpilib", "2026.1.0");
            CreateInstalledManifest(installDir, "navx", "2026.1.0");

            var pm = CreatePackageManager(manifests, index: index, installDir: installDir);
            var plan = await pm.PlanUpdateAsync();

            // Only wpilib is outdated (2026.1.0 -> 2026.2.0)
            Assert.Single(plan.Steps);
            Assert.Equal("wpilib", plan.Steps[0].PackageId);
            Assert.Equal(InstallAction.Update, plan.Steps[0].Action);
        }
        finally
        {
            if (Directory.Exists(installDir))
            {
                Directory.Delete(installDir, true);
            }
        }
    }

    [Fact]
    public async Task PlanUninstallAsync_BlocksWhenReverseDependenciesExist()
    {
        var manifests = new Dictionary<string, PackageManifest>
        {
            ["wpilib"] = MakeManifest("wpilib", "2026.1.0", size: 5000),
            ["navx"] = MakeManifest("navx", "2026.1.0", size: 2000,
                deps: new[] { MakeDep("wpilib") })
        };

        var installDir = Path.Combine(TestInstallDir, Guid.NewGuid().ToString());

        try
        {
            CreateInstalledManifest(installDir, "wpilib", "2026.1.0");
            CreateInstalledManifest(installDir, "navx", "2026.1.0");

            var pm = CreatePackageManager(manifests, installDir: installDir);

            // Trying to uninstall wpilib should fail because navx depends on it
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => pm.PlanUninstallAsync(new[] { "wpilib" }));

            Assert.Contains("required by", ex.Message);
            Assert.Contains("navx", ex.Message);
        }
        finally
        {
            if (Directory.Exists(installDir))
            {
                Directory.Delete(installDir, true);
            }
        }
    }

    private static PackageManager CreatePackageManager(
        Dictionary<string, PackageManifest> manifests,
        RegistryIndex? index = null,
        Dictionary<string, BundleDefinition>? bundles = null,
        string? installDir = null)
    {
        var effectiveInstallDir = installDir ?? Path.Combine(TestInstallDir, Guid.NewGuid().ToString());
        var registry = new FakeRegistryClient(manifests, index, bundles);
        var downloadManager = new FakeDownloadManager();
        var installEngine = new FakeInstallEngine();
        var platformService = new FakePlatformService();
        var settingsProvider = new FakeSettingsProvider(effectiveInstallDir);

        return new PackageManager(
            registry, downloadManager, installEngine, platformService, settingsProvider);
    }

    private static void CreateInstalledManifest(string installDir, string packageId, string version)
    {
        var pkgDir = Path.Combine(installDir, packageId);
        Directory.CreateDirectory(pkgDir);

        var manifest = new InstallManifest
        {
            PackageId = packageId,
            Version = version,
            Season = "2026",
            InstalledAt = DateTime.UtcNow,
            InstallPath = pkgDir,
            InstalledFiles = Array.Empty<string>(),
            Platform = "windows-x64"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(manifest);
        File.WriteAllText(Path.Combine(pkgDir, ".install-manifest.json"), json);
    }

    private static PackageManifest MakeManifest(
        string id,
        string version,
        long size = 0,
        PackageDependency[]? deps = null)
    {
        var manifest = new PackageManifest
        {
            Id = id,
            Name = id,
            Version = version,
            Season = 2026,
            Dependencies = deps?.ToList() ?? [],
            Artifacts = new Dictionary<string, PackageArtifact>()
        };

        if (size > 0)
        {
            manifest.Artifacts["windows-x64"] = new PackageArtifact
            {
                Url = $"https://example.com/{id}-{version}.zip",
                Size = size
            };
        }

        return manifest;
    }

    private static PackageDependency MakeDep(string id)
    {
        return new PackageDependency
        {
            Id = id,
            Type = DependencyType.Required
        };
    }
}

internal class FakeDownloadManager : IDownloadManager
{
    public Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(new DownloadResult(true, request.TargetPath));
    }

    public Task<IReadOnlyList<DownloadResult>> DownloadParallelAsync(
        IReadOnlyList<DownloadRequest> requests,
        int maxConcurrency = 4,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var results = requests.Select(r => new DownloadResult(true, r.TargetPath)).ToList();
        return Task.FromResult<IReadOnlyList<DownloadResult>>(results);
    }
}

internal class FakeInstallEngine : IInstallEngine
{
    public Task<IReadOnlyList<string>> ExtractAsync(
        string archivePath,
        string destinationPath,
        string? archiveType = null,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task RunPostInstallAsync(
        IReadOnlyList<PostInstallAction> actions,
        string installPath,
        CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task RecordInstallAsync(
        InstalledPackage package,
        CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task InstallMavenBundleAsync(
        string mavenZipPath,
        string mavenCacheDir,
        string? vendordepJsonPath,
        string? vendordepJsonUrl,
        CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task RemoveInstalledFilesAsync(
        InstalledPackage package,
        CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}

internal class FakePlatformService : IPlatformService
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

internal class FakeSettingsProvider : ISettingsProvider
{
    private readonly string _installDir;

    public FakeSettingsProvider(string installDir)
    {
        _installDir = installDir;
    }

    public Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new AppSettings
        {
            InstallDirectory = _installDir,
            CacheDirectory = Path.Combine(_installDir, ".cache")
        });
    }

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
}
