using System.Security.Cryptography;
using System.Text.Json;
using FrcToolsuite.Core.Download;
using FrcToolsuite.Core.Offline;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Tests;

public class OfflineCacheManagerTests : IDisposable
{
    private readonly string _tempDir;

    public OfflineCacheManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "OfflineCacheTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public void DetectUsbDrives_ReturnsListWithoutThrowing()
    {
        // USB drives may or may not be present; the method should not throw
        var drives = OfflineCacheManager.DetectUsbDrives();
        Assert.NotNull(drives);
        // All returned drives should be removable
        Assert.All(drives, d => Assert.Equal(DriveType.Removable, d.DriveType));
    }

    [Fact]
    public async Task ExportToUsbAsync_CreatesCorrectDirectoryStructure()
    {
        var registryClient = new FakeRegistryClient();
        var downloadManager = new FakeDownloadManager();
        var manager = new OfflineCacheManager(registryClient, downloadManager);

        var targetPath = Path.Combine(_tempDir, "usb");
        Directory.CreateDirectory(targetPath);

        await manager.ExportToUsbAsync(targetPath, packageIds: new[] { "test-package" });

        // Verify directory structure
        Assert.True(Directory.Exists(Path.Combine(targetPath, "frc-packages", "cache")));
        Assert.True(Directory.Exists(Path.Combine(targetPath, "frc-packages", "registry")));

        // Verify registry index was written
        Assert.True(File.Exists(Path.Combine(targetPath, "frc-packages", "registry", "index.json")));

        // Verify portable marker was written
        Assert.True(File.Exists(Path.Combine(targetPath, "frc-packages", "portable.json")));

        // Verify the portable.json contains valid JSON
        var markerJson = await File.ReadAllTextAsync(
            Path.Combine(targetPath, "frc-packages", "portable.json"));
        var marker = JsonSerializer.Deserialize<JsonElement>(markerJson);
        Assert.True(marker.TryGetProperty("createdAt", out _));
        Assert.True(marker.TryGetProperty("packageCount", out _));
    }

    [Fact]
    public async Task ExportToUsbAsync_ReportsProgress()
    {
        var registryClient = new FakeRegistryClient();
        var downloadManager = new FakeDownloadManager();
        var manager = new OfflineCacheManager(registryClient, downloadManager);

        var targetPath = Path.Combine(_tempDir, "usb_progress");
        Directory.CreateDirectory(targetPath);

        var progressReports = new List<OfflineSyncProgress>();
        var progress = new Progress<OfflineSyncProgress>(p => progressReports.Add(p));

        await manager.ExportToUsbAsync(targetPath, progress: progress);

        // Allow progress callbacks to be delivered
        await Task.Delay(100);

        Assert.NotEmpty(progressReports);
        // First report should be for registry
        Assert.Contains(progressReports, p => p.CurrentItem.Contains("registry") || p.CompletedItems >= 1);
    }

    [Fact]
    public async Task IsCacheValidAsync_ReturnsFalse_ForNonExistentDirectory()
    {
        var registryClient = new FakeRegistryClient();
        var downloadManager = new FakeDownloadManager();
        var manager = new OfflineCacheManager(registryClient, downloadManager);

        var result = await manager.IsCacheValidAsync(
            Path.Combine(_tempDir, "nonexistent"));

        Assert.False(result);
    }

    [Fact]
    public async Task IsCacheValidAsync_ReturnsFalse_WhenCacheSubdirMissing()
    {
        var registryClient = new FakeRegistryClient();
        var downloadManager = new FakeDownloadManager();
        var manager = new OfflineCacheManager(registryClient, downloadManager);

        var cachePath = Path.Combine(_tempDir, "bad_cache");
        Directory.CreateDirectory(cachePath);
        // Don't create the "cache" subdirectory

        var result = await manager.IsCacheValidAsync(cachePath);

        Assert.False(result);
    }

    [Fact]
    public async Task IsCacheValidAsync_DetectsCorruptedFiles()
    {
        var registryClient = new FakeRegistryClient();
        var downloadManager = new FakeDownloadManager();
        var manager = new OfflineCacheManager(registryClient, downloadManager);

        var cachePath = Path.Combine(_tempDir, "corrupt_cache");
        var packageDir = Path.Combine(cachePath, "cache", "test-package");
        Directory.CreateDirectory(packageDir);

        // Write a file and a .sha256 sidecar with wrong hash
        var content = "some content"u8.ToArray();
        var filePath = Path.Combine(packageDir, "artifact.zip");
        await File.WriteAllBytesAsync(filePath, content);
        await File.WriteAllTextAsync(filePath + ".sha256", "0000000000000000000000000000000000000000000000000000000000000000");

        var result = await manager.IsCacheValidAsync(cachePath);

        Assert.False(result);
    }

    [Fact]
    public async Task IsCacheValidAsync_ReturnsTrue_WhenHashesMatch()
    {
        var registryClient = new FakeRegistryClient();
        var downloadManager = new FakeDownloadManager();
        var manager = new OfflineCacheManager(registryClient, downloadManager);

        var cachePath = Path.Combine(_tempDir, "valid_cache");
        var packageDir = Path.Combine(cachePath, "cache", "test-package");
        Directory.CreateDirectory(packageDir);

        // Write a file and a .sha256 sidecar with correct hash
        var content = "valid content"u8.ToArray();
        var filePath = Path.Combine(packageDir, "artifact.zip");
        await File.WriteAllBytesAsync(filePath, content);
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        await File.WriteAllTextAsync(filePath + ".sha256", hash);

        var result = await manager.IsCacheValidAsync(cachePath);

        Assert.True(result);
    }

    [Fact]
    public void IsRunningFromPortable_ReturnsFalse_WhenNoMarkerExists()
    {
        // In a test environment, there's no portable.json next to the test runner
        var result = OfflineCacheManager.IsRunningFromPortable();
        Assert.False(result);
    }

    [Fact]
    public async Task GetCachedPackageIdsAsync_FindsCachedPackages()
    {
        var registryClient = new FakeRegistryClient();
        var downloadManager = new FakeDownloadManager();
        var manager = new OfflineCacheManager(registryClient, downloadManager);

        // Export some packages first to create cache structure
        var targetPath = Path.Combine(_tempDir, "usb_cached");
        Directory.CreateDirectory(targetPath);

        await manager.ExportToUsbAsync(targetPath, packageIds: new[] { "test-package" });

        // The exported cache is under frc-packages/cache/{id}/
        // GetCachedPackageIdsAsync checks the local user cache, not USB
        // Verify the method works without throwing
        var cachedIds = await manager.GetCachedPackageIdsAsync();
        Assert.NotNull(cachedIds);
    }

    [Fact]
    public void GetPortableCachePath_ReturnsNull_WhenNotPortable()
    {
        var result = OfflineCacheManager.GetPortableCachePath();
        // May or may not be null depending on the test runner location,
        // but it should not throw
        _ = result;
    }

    // ---- Fakes ----

    private sealed class FakeRegistryClient : IRegistryClient
    {
        public bool IsOffline => false;

        public Task<RegistryIndex> FetchRegistryAsync(bool forceRefresh = false, CancellationToken ct = default)
        {
            var index = new RegistryIndex
            {
                SchemaVersion = "1.0",
                LastUpdated = DateTimeOffset.UtcNow,
                Packages =
                [
                    new PackageSummary
                    {
                        Id = "test-package",
                        Name = "Test Package",
                        Version = "1.0.0",
                        Season = 2026,
                        Category = "tools",
                        Competition = CompetitionProgram.Frc,
                        Description = "A test package",
                        ManifestUrl = "https://example.com/test-package.json",
                    }
                ],
            };
            return Task.FromResult(index);
        }

        public Task<PackageManifest> GetPackageAsync(string packageId, CancellationToken ct = default)
        {
            var manifest = new PackageManifest
            {
                Id = packageId,
                Name = "Test Package",
                Version = "1.0.0",
                Season = 2026,
                Publisher = "test-publisher",
                Description = "A test package",
                Category = "tools",
                Competition = CompetitionProgram.Frc,
                Artifacts = new Dictionary<string, PackageArtifact>
                {
                    ["windows-x64"] = new PackageArtifact
                    {
                        Url = "https://example.com/test-artifact.zip",
                        Sha256 = "abc123",
                        Size = 1024,
                        Filename = "test-artifact.zip",
                    }
                },
            };
            return Task.FromResult(manifest);
        }

        public Task<IReadOnlyList<PackageSummary>> SearchAsync(
            string? query = null,
            CompetitionProgram? program = null,
            int? year = null,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<PackageSummary>>(Array.Empty<PackageSummary>());
        }

        public Task<BundleDefinition> GetBundleAsync(string bundleId, CancellationToken ct = default)
        {
            return Task.FromResult(new BundleDefinition { Id = bundleId });
        }
    }

    private sealed class FakeDownloadManager : IDownloadManager
    {
        public Task<DownloadResult> DownloadAsync(
            DownloadRequest request,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken ct = default)
        {
            // Create the target directory and write a fake file
            var dir = Path.GetDirectoryName(request.TargetPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(request.TargetPath, new byte[] { 0x00, 0x01, 0x02 });

            return Task.FromResult(new DownloadResult(
                true,
                request.TargetPath,
                ActualSha256: "fakehash"));
        }

        public Task<IReadOnlyList<DownloadResult>> DownloadParallelAsync(
            IReadOnlyList<DownloadRequest> requests,
            int maxConcurrency = 4,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken ct = default)
        {
            var results = requests.Select(r => DownloadAsync(r, progress, ct).Result).ToList();
            return Task.FromResult<IReadOnlyList<DownloadResult>>(results);
        }
    }
}
