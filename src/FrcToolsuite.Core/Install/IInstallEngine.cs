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
}
