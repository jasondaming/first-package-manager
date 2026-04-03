using System.Security.Cryptography;
using System.Text.Json;
using FrcToolsuite.Core.Download;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Offline;

/// <summary>
/// Manages offline package caching for USB export/import and portable mode detection.
/// </summary>
public class OfflineCacheManager : IOfflineCacheManager
{
    private const string FrcPackagesDir = "frc-packages";
    private const string CacheSubDir = "cache";
    private const string RegistrySubDir = "registry";
    private const string PortableMarkerFile = "portable.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IRegistryClient _registryClient;
    private readonly IDownloadManager _downloadManager;

    public OfflineCacheManager(IRegistryClient registryClient, IDownloadManager downloadManager)
    {
        _registryClient = registryClient ?? throw new ArgumentNullException(nameof(registryClient));
        _downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
    }

    public async Task ExportToUsbAsync(
        string targetPath,
        IReadOnlyList<string>? packageIds = null,
        IProgress<OfflineSyncProgress>? progress = null,
        CancellationToken ct = default)
    {
        var cacheDir = Path.Combine(targetPath, FrcPackagesDir, CacheSubDir);
        var registryDir = Path.Combine(targetPath, FrcPackagesDir, RegistrySubDir);

        Directory.CreateDirectory(cacheDir);
        Directory.CreateDirectory(registryDir);

        // Fetch and save registry index
        var index = await _registryClient.FetchRegistryAsync(ct: ct).ConfigureAwait(false);
        var indexJson = JsonSerializer.Serialize(index, JsonOptions);
        await File.WriteAllTextAsync(
            Path.Combine(registryDir, "index.json"), indexJson, ct).ConfigureAwait(false);

        // Determine which packages to export
        var packagesToExport = packageIds is { Count: > 0 }
            ? index.Packages.Where(p =>
                packageIds.Any(id => string.Equals(id, p.Id, StringComparison.OrdinalIgnoreCase)))
                .ToList()
            : index.Packages;

        var totalItems = packagesToExport.Count + 1; // +1 for registry
        var completedItems = 1; // Registry already done
        long totalBytesTransferred = 0;

        progress?.Report(new OfflineSyncProgress(
            completedItems, totalItems, "registry/index.json", indexJson.Length, 0));

        // Download each package's artifacts to the cache directory
        foreach (var packageSummary in packagesToExport)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var manifest = await _registryClient.GetPackageAsync(packageSummary.Id, ct)
                    .ConfigureAwait(false);

                // Save manifest to registry dir
                var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
                await File.WriteAllTextAsync(
                    Path.Combine(registryDir, $"{SanitizeId(manifest.Id)}.json"),
                    manifestJson, ct).ConfigureAwait(false);

                // Download each artifact
                foreach (var (platformKey, artifact) in manifest.Artifacts)
                {
                    ct.ThrowIfCancellationRequested();

                    var fileName = artifact.Filename
                        ?? Path.GetFileName(new Uri(artifact.Url).AbsolutePath);
                    var artifactPath = Path.Combine(
                        cacheDir, SanitizeId(manifest.Id), fileName);

                    // Skip if already cached with correct hash
                    if (File.Exists(artifactPath) && !string.IsNullOrEmpty(artifact.Sha256))
                    {
                        var existingHash = await ComputeFileHashAsync(artifactPath, ct)
                            .ConfigureAwait(false);
                        if (string.Equals(existingHash, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    var request = new DownloadRequest(
                        artifact.Url,
                        artifactPath,
                        ExpectedSha256: artifact.Sha256,
                        ExpectedSize: artifact.Size,
                        Mirrors: artifact.Mirrors.Count > 0 ? artifact.Mirrors.ToArray() : null);

                    var result = await _downloadManager.DownloadAsync(request, ct: ct)
                        .ConfigureAwait(false);

                    if (result.Success)
                    {
                        totalBytesTransferred += artifact.Size;
                    }
                }
            }
            catch (KeyNotFoundException)
            {
                // Package manifest not found; skip
            }

            completedItems++;
            progress?.Report(new OfflineSyncProgress(
                completedItems, totalItems, packageSummary.Id,
                totalBytesTransferred, 0));
        }

        // Copy the running executable to the target if possible
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            var exeTargetPath = Path.Combine(targetPath, FrcPackagesDir, Path.GetFileName(exePath));
            File.Copy(exePath, exeTargetPath, overwrite: true);
        }

        // Write portable marker
        var portableInfo = new
        {
            createdAt = DateTimeOffset.UtcNow,
            packageCount = packagesToExport.Count,
        };
        await File.WriteAllTextAsync(
            Path.Combine(targetPath, FrcPackagesDir, PortableMarkerFile),
            JsonSerializer.Serialize(portableInfo, JsonOptions), ct).ConfigureAwait(false);
    }

    public async Task ImportFromUsbAsync(
        string sourcePath,
        IProgress<OfflineSyncProgress>? progress = null,
        CancellationToken ct = default)
    {
        var cacheDir = Path.Combine(sourcePath, FrcPackagesDir, CacheSubDir);
        var registryDir = Path.Combine(sourcePath, FrcPackagesDir, RegistrySubDir);

        if (!Directory.Exists(cacheDir))
        {
            throw new DirectoryNotFoundException(
                $"Cache directory not found at '{cacheDir}'. Is this a valid FRC offline cache?");
        }

        var localCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".frctoolsuite", "cache");
        Directory.CreateDirectory(localCacheDir);

        // Copy registry data
        if (Directory.Exists(registryDir))
        {
            var registryFiles = Directory.GetFiles(registryDir, "*.json");
            foreach (var file in registryFiles)
            {
                ct.ThrowIfCancellationRequested();
                var destPath = Path.Combine(localCacheDir, Path.GetFileName(file));
                var bytes = await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false);
                await File.WriteAllBytesAsync(destPath, bytes, ct).ConfigureAwait(false);
            }
        }

        // Copy cached packages
        var packageDirs = Directory.GetDirectories(cacheDir);
        var totalItems = packageDirs.Length;
        var completedItems = 0;
        long totalBytes = 0;

        foreach (var packageDir in packageDirs)
        {
            ct.ThrowIfCancellationRequested();

            var packageName = Path.GetFileName(packageDir);
            var destDir = Path.Combine(localCacheDir, "packages", packageName);
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(packageDir))
            {
                ct.ThrowIfCancellationRequested();
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                var bytes = await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false);
                await File.WriteAllBytesAsync(destFile, bytes, ct).ConfigureAwait(false);
                totalBytes += bytes.Length;
            }

            completedItems++;
            progress?.Report(new OfflineSyncProgress(
                completedItems, totalItems, packageName, totalBytes, 0));
        }
    }

    public Task<IReadOnlyList<string>> GetCachedPackageIdsAsync(CancellationToken ct = default)
    {
        var cacheDir = GetLocalCachePath();
        var result = new List<string>();

        if (Directory.Exists(cacheDir))
        {
            foreach (var dir in Directory.GetDirectories(cacheDir))
            {
                ct.ThrowIfCancellationRequested();
                var dirName = Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(dirName))
                {
                    result.Add(dirName);
                }
            }
        }

        // Also check portable cache
        var portableCachePath = GetPortableCachePath();
        if (portableCachePath is not null && Directory.Exists(portableCachePath))
        {
            foreach (var dir in Directory.GetDirectories(portableCachePath))
            {
                ct.ThrowIfCancellationRequested();
                var dirName = Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(dirName) && !result.Contains(dirName))
                {
                    result.Add(dirName);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(result.AsReadOnly());
    }

    public async Task<bool> IsCacheValidAsync(string cachePath, CancellationToken ct = default)
    {
        if (!Directory.Exists(cachePath))
        {
            return false;
        }

        var registryDir = Path.Combine(cachePath, RegistrySubDir);
        if (registryDir.Contains(FrcPackagesDir, StringComparison.OrdinalIgnoreCase))
        {
            registryDir = Path.Combine(cachePath, RegistrySubDir);
        }
        else
        {
            // cachePath might be the frc-packages dir itself
            registryDir = Path.Combine(cachePath, RegistrySubDir);
        }

        var cacheSubDir = Path.Combine(cachePath, CacheSubDir);

        if (!Directory.Exists(cacheSubDir))
        {
            return false;
        }

        // Validate each package directory by checking that manifest files exist
        // and any file with a corresponding .sha256 sidecar matches
        foreach (var packageDir in Directory.GetDirectories(cacheSubDir))
        {
            ct.ThrowIfCancellationRequested();

            foreach (var file in Directory.GetFiles(packageDir))
            {
                ct.ThrowIfCancellationRequested();

                var sha256File = file + ".sha256";
                if (File.Exists(sha256File))
                {
                    var expectedHash = (await File.ReadAllTextAsync(sha256File, ct)
                        .ConfigureAwait(false)).Trim();
                    var actualHash = await ComputeFileHashAsync(file, ct).ConfigureAwait(false);

                    if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }
        }

        // Also validate against registry manifests if available
        if (Directory.Exists(registryDir))
        {
            foreach (var manifestFile in Directory.GetFiles(registryDir, "*.json"))
            {
                ct.ThrowIfCancellationRequested();

                if (Path.GetFileName(manifestFile) == "index.json")
                {
                    continue;
                }

                try
                {
                    var json = await File.ReadAllTextAsync(manifestFile, ct).ConfigureAwait(false);
                    var manifest = JsonSerializer.Deserialize<PackageManifest>(json, JsonOptions);
                    if (manifest is null)
                    {
                        continue;
                    }

                    var packageCacheDir = Path.Combine(cacheSubDir, SanitizeId(manifest.Id));
                    if (!Directory.Exists(packageCacheDir))
                    {
                        continue;
                    }

                    foreach (var (_, artifact) in manifest.Artifacts)
                    {
                        if (string.IsNullOrEmpty(artifact.Sha256))
                        {
                            continue;
                        }

                        var fileName = artifact.Filename
                            ?? Path.GetFileName(new Uri(artifact.Url).AbsolutePath);
                        var filePath = Path.Combine(packageCacheDir, fileName);

                        if (!File.Exists(filePath))
                        {
                            continue;
                        }

                        var actualHash = await ComputeFileHashAsync(filePath, ct)
                            .ConfigureAwait(false);
                        if (!string.Equals(actualHash, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                }
                catch (JsonException)
                {
                    // Not a valid manifest file, skip
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Detects removable USB drives available on the system.
    /// </summary>
    public static IReadOnlyList<DriveInfo> DetectUsbDrives()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Checks whether the application is running from a portable (USB) installation.
    /// </summary>
    public static bool IsRunningFromPortable()
    {
        var exeDir = AppContext.BaseDirectory;
        var markerPath = Path.Combine(exeDir, PortableMarkerFile);

        if (File.Exists(markerPath))
        {
            return true;
        }

        // Check parent directory (in case exe is in a subdirectory of frc-packages)
        var parentDir = Path.GetDirectoryName(exeDir.TrimEnd(Path.DirectorySeparatorChar));
        if (parentDir is not null)
        {
            markerPath = Path.Combine(parentDir, PortableMarkerFile);
            if (File.Exists(markerPath))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the cache path for a portable installation, or null if not running in portable mode.
    /// </summary>
    public static string? GetPortableCachePath()
    {
        var exeDir = AppContext.BaseDirectory;

        // Check current directory
        var cachePath = Path.Combine(exeDir, CacheSubDir);
        if (Directory.Exists(cachePath))
        {
            return cachePath;
        }

        // Check parent directory
        var parentDir = Path.GetDirectoryName(exeDir.TrimEnd(Path.DirectorySeparatorChar));
        if (parentDir is not null)
        {
            cachePath = Path.Combine(parentDir, CacheSubDir);
            if (Directory.Exists(cachePath))
            {
                return cachePath;
            }
        }

        return null;
    }

    private static string GetLocalCachePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".frctoolsuite", "cache", "packages");
    }

    private static string SanitizeId(string packageId)
    {
        return packageId.Replace("/", "_").Replace("\\", "_");
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
    {
        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hashBytes = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
