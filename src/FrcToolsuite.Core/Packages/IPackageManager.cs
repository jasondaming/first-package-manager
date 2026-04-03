using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Packages;

public interface IPackageManager
{
    Task<InstallPlan> PlanInstallAsync(
        IReadOnlyList<string> packageIds,
        CancellationToken ct = default);

    Task<InstallPlan> PlanBundleInstallAsync(
        string bundleId,
        IReadOnlyList<string>? optionalPackageIds = null,
        CancellationToken ct = default);

    Task<InstallPlan> PlanUpdateAsync(
        IReadOnlyList<string>? packageIds = null,
        CancellationToken ct = default);

    Task<InstallPlan> PlanUninstallAsync(
        IReadOnlyList<string> packageIds,
        CancellationToken ct = default);

    Task ExecutePlanAsync(
        InstallPlan plan,
        IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<InstalledPackage>> GetInstalledPackagesAsync(
        CancellationToken ct = default);

    Task<IReadOnlyList<PackageUpdateInfo>> CheckForUpdatesAsync(
        CancellationToken ct = default);
}

public record PackageUpdateInfo(
    string PackageId,
    string InstalledVersion,
    string AvailableVersion);
