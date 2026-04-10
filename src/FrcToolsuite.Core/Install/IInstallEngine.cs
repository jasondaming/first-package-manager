using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Install;

public interface IInstallEngine
{
    Task<IReadOnlyList<string>> ExtractAsync(
        string archivePath,
        string destinationPath,
        string? archiveType = null,
        CancellationToken ct = default);

    Task RunPostInstallAsync(
        IReadOnlyList<PostInstallAction> actions,
        string installPath,
        CancellationToken ct = default);

    Task RecordInstallAsync(
        InstalledPackage package,
        CancellationToken ct = default);

    Task RemoveInstalledFilesAsync(
        InstalledPackage package,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts a Maven bundle zip into the local Maven cache directory,
    /// optionally downloads a vendordep JSON, and runs the metadata fixer.
    /// </summary>
    Task InstallMavenBundleAsync(
        string mavenZipPath,
        string mavenCacheDir,
        string? vendordepJsonPath,
        string? vendordepJsonUrl,
        CancellationToken ct = default);
}
