using FrcToolsuite.Core.Configuration;
using FrcToolsuite.Core.Download;
using FrcToolsuite.Core.Install;
using FrcToolsuite.Core.Platform;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Packages;

public class PackageManager : IPackageManager
{
    private readonly IRegistryClient _registry;
    private readonly IDownloadManager _downloadManager;
    private readonly IInstallEngine _installEngine;
    private readonly IPlatformService _platformService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly DependencyResolver _resolver;

    public PackageManager(
        IRegistryClient registry,
        IDownloadManager downloadManager,
        IInstallEngine installEngine,
        IPlatformService platformService,
        ISettingsProvider settingsProvider)
    {
        _registry = registry;
        _downloadManager = downloadManager;
        _installEngine = installEngine;
        _platformService = platformService;
        _settingsProvider = settingsProvider;
        _resolver = new DependencyResolver();
    }

    public async Task<InstallPlan> PlanInstallAsync(
        IReadOnlyList<string> packageIds,
        CancellationToken ct = default)
    {
        var platform = _platformService.GetPlatformId();
        var resolved = await _resolver.ResolveAsync(packageIds, platform, _registry, ct);

        var plan = new InstallPlan();
        foreach (var manifest in resolved)
        {
            var step = CreateInstallStep(manifest, platform, InstallAction.Install);
            plan.Steps.Add(step);
        }

        return plan;
    }

    public async Task<InstallPlan> PlanBundleInstallAsync(
        string bundleId,
        IReadOnlyList<string>? optionalPackageIds = null,
        CancellationToken ct = default)
    {
        var bundle = await _registry.GetBundleAsync(bundleId, ct);
        var optionalSet = new HashSet<string>(
            optionalPackageIds ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var packageIds = new List<string>();
        foreach (var pkgRef in bundle.Packages)
        {
            switch (pkgRef.Inclusion)
            {
                case PackageInclusion.Required:
                case PackageInclusion.Default:
                    packageIds.Add(pkgRef.Id);
                    break;
                case PackageInclusion.Optional:
                    if (optionalSet.Contains(pkgRef.Id))
                    {
                        packageIds.Add(pkgRef.Id);
                    }
                    break;
            }
        }

        return await PlanInstallAsync(packageIds, ct);
    }

    public async Task<InstallPlan> PlanUpdateAsync(
        IReadOnlyList<string>? packageIds = null,
        CancellationToken ct = default)
    {
        var installed = await GetInstalledPackagesAsync(ct);
        var updates = await CheckForUpdatesAsync(ct);

        var updateSet = packageIds != null
            ? new HashSet<string>(packageIds, StringComparer.OrdinalIgnoreCase)
            : null;

        var plan = new InstallPlan();
        var platform = _platformService.GetPlatformId();

        foreach (var update in updates)
        {
            if (updateSet != null && !updateSet.Contains(update.PackageId))
            {
                continue;
            }

            var manifest = await _registry.GetPackageAsync(update.PackageId, ct);
            var step = CreateInstallStep(manifest, platform, InstallAction.Update);
            plan.Steps.Add(step);
        }

        return plan;
    }

    public async Task<InstallPlan> PlanUninstallAsync(
        IReadOnlyList<string> packageIds,
        CancellationToken ct = default)
    {
        var installed = await GetInstalledPackagesAsync(ct);
        var uninstallSet = new HashSet<string>(packageIds, StringComparer.OrdinalIgnoreCase);

        // Check reverse dependencies: ensure no remaining installed package depends on packages being removed
        var remainingPackages = installed
            .Where(p => !uninstallSet.Contains(p.PackageId))
            .ToList();

        foreach (var remaining in remainingPackages)
        {
            var manifest = await _registry.GetPackageAsync(remaining.PackageId, ct);
            foreach (var dep in manifest.Dependencies)
            {
                if (dep.Type == DependencyType.Required && uninstallSet.Contains(dep.Id))
                {
                    throw new InvalidOperationException(
                        $"Cannot uninstall '{dep.Id}' because it is required by installed package '{remaining.PackageId}'.");
                }
            }
        }

        var plan = new InstallPlan();
        foreach (var id in packageIds)
        {
            var pkg = installed.FirstOrDefault(p =>
                string.Equals(p.PackageId, id, StringComparison.OrdinalIgnoreCase));
            if (pkg != null)
            {
                plan.Steps.Add(new InstallStep
                {
                    PackageId = pkg.PackageId,
                    Version = pkg.Version,
                    Action = InstallAction.Uninstall
                });
            }
        }

        return plan;
    }

    public async Task ExecutePlanAsync(
        InstallPlan plan,
        IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var settings = await _settingsProvider.LoadAsync(ct);
        var platform = _platformService.GetPlatformId();

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var step = plan.Steps[i];

            if (step.Action == InstallAction.Uninstall)
            {
                var installed = await GetInstalledPackagesAsync(ct);
                var pkg = installed.FirstOrDefault(p =>
                    string.Equals(p.PackageId, step.PackageId, StringComparison.OrdinalIgnoreCase));
                if (pkg != null)
                {
                    await _installEngine.RemoveInstalledFilesAsync(pkg, ct);
                }
                continue;
            }

            var installDir = Path.Combine(settings.InstallDirectory, step.PackageId);

            // Download
            progress?.Report(new InstallProgress(
                i + 1, plan.Steps.Count, step.PackageId, 0, step.DownloadSize, InstallPhase.Downloading));

            if (step.ArtifactUrl != null)
            {
                var downloadPath = Path.Combine(settings.CacheDirectory, $"{step.PackageId}-{step.Version}.download");
                Directory.CreateDirectory(settings.CacheDirectory);

                var downloadResult = await _downloadManager.DownloadAsync(
                    new DownloadRequest(step.ArtifactUrl, downloadPath),
                    new Progress<DownloadProgress>(dp =>
                        progress?.Report(new InstallProgress(
                            i + 1, plan.Steps.Count, step.PackageId,
                            dp.BytesDownloaded, dp.TotalBytes, InstallPhase.Downloading))),
                    ct);

                if (!downloadResult.Success)
                {
                    throw new InvalidOperationException(
                        $"Download failed for '{step.PackageId}': {downloadResult.Error}");
                }

                // Extract
                progress?.Report(new InstallProgress(
                    i + 1, plan.Steps.Count, step.PackageId, step.DownloadSize, step.DownloadSize,
                    InstallPhase.Extracting));

                var extractedFiles = await _installEngine.ExtractAsync(
                    downloadResult.FilePath, installDir, null, ct);

                // Post-install
                progress?.Report(new InstallProgress(
                    i + 1, plan.Steps.Count, step.PackageId, step.DownloadSize, step.DownloadSize,
                    InstallPhase.Configuring));

                var manifest = await _registry.GetPackageAsync(step.PackageId, ct);
                if (manifest.Install?.PostInstall.Count > 0)
                {
                    var platformActions = manifest.Install.PostInstall
                        .Where(a => a.Platform == null || a.Platform == platform)
                        .ToList();
                    await _installEngine.RunPostInstallAsync(platformActions, installDir, ct);
                }

                // Record
                var installedPackage = new InstalledPackage(
                    step.PackageId,
                    step.Version,
                    manifest.Season,
                    DateTimeOffset.UtcNow,
                    installDir,
                    extractedFiles.ToArray());

                await _installEngine.RecordInstallAsync(installedPackage, ct);
            }
        }
    }

    public async Task<IReadOnlyList<InstalledPackage>> GetInstalledPackagesAsync(
        CancellationToken ct = default)
    {
        var settings = await _settingsProvider.LoadAsync(ct);
        var packages = new List<InstalledPackage>();

        if (!Directory.Exists(settings.InstallDirectory))
        {
            return packages;
        }

        foreach (var dir in Directory.GetDirectories(settings.InstallDirectory))
        {
            ct.ThrowIfCancellationRequested();
            var manifestPath = Path.Combine(dir, ".install-manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var json = await File.ReadAllTextAsync(manifestPath, ct);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<InstallManifest>(json);
            if (manifest == null)
            {
                continue;
            }

            int.TryParse(manifest.Season, out var season);

            packages.Add(new InstalledPackage(
                manifest.PackageId,
                manifest.Version,
                season,
                new DateTimeOffset(manifest.InstalledAt, TimeSpan.Zero),
                manifest.InstallPath,
                manifest.InstalledFiles));
        }

        return packages;
    }

    public async Task<IReadOnlyList<PackageUpdateInfo>> CheckForUpdatesAsync(
        CancellationToken ct = default)
    {
        var installed = await GetInstalledPackagesAsync(ct);
        var index = await _registry.FetchRegistryAsync(false, ct);
        var updates = new List<PackageUpdateInfo>();

        foreach (var pkg in installed)
        {
            var summary = index.Packages.FirstOrDefault(p =>
                string.Equals(p.Id, pkg.PackageId, StringComparison.OrdinalIgnoreCase));

            if (summary == null)
            {
                continue;
            }

            var installedVersion = PackageVersion.Parse(pkg.Version);
            var availableVersion = PackageVersion.Parse(summary.Version);

            if (availableVersion > installedVersion)
            {
                updates.Add(new PackageUpdateInfo(
                    pkg.PackageId, pkg.Version, summary.Version));
            }
        }

        return updates;
    }

    private static InstallStep CreateInstallStep(
        PackageManifest manifest,
        string platform,
        InstallAction action)
    {
        string? artifactUrl = null;
        long downloadSize = 0;

        if (manifest.Artifacts.TryGetValue(platform, out var artifact))
        {
            artifactUrl = artifact.Url;
            downloadSize = artifact.Size;
        }

        return new InstallStep
        {
            PackageId = manifest.Id,
            Version = manifest.Version,
            Action = action,
            ArtifactUrl = artifactUrl,
            DownloadSize = downloadSize,
            RequiresAdmin = manifest.Install?.RequiresAdmin ?? false
        };
    }
}
