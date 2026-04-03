using FrcToolsuite.Core.Download;

namespace FrcToolsuite.Core.Update;

public interface ISelfUpdater
{
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default);
    Task DownloadAndApplyUpdateAsync(UpdateInfo update, IProgress<DownloadProgress> progress, CancellationToken ct = default);
    string CurrentVersion { get; }
}

public record UpdateInfo(
    string Version,
    string DownloadUrl,
    string Sha256,
    long Size,
    string? ReleaseNotes);
