namespace FrcToolsuite.Core.Registry;

public interface IRegistryClient
{
    Task<RegistryIndex> FetchRegistryAsync(bool forceRefresh = false, CancellationToken ct = default);

    Task<PackageManifest> GetPackageAsync(string packageId, CancellationToken ct = default);

    Task<IReadOnlyList<PackageSummary>> SearchAsync(
        string? query = null,
        CompetitionProgram? program = null,
        int? year = null,
        CancellationToken ct = default);

    Task<BundleDefinition> GetBundleAsync(string bundleId, CancellationToken ct = default);

    bool IsOffline { get; }
}
