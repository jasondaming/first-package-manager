namespace FrcToolsuite.Core.Download;

public interface IDownloadManager
{
    Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<DownloadResult>> DownloadParallelAsync(
        IReadOnlyList<DownloadRequest> requests,
        int maxConcurrency = 4,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default);
}

public record DownloadRequest(
    string Url,
    string TargetPath,
    string? ExpectedSha256 = null,
    long? ExpectedSize = null,
    string[]? Mirrors = null);

public record DownloadProgress(
    long BytesDownloaded,
    long TotalBytes,
    string CurrentFile);

public record DownloadResult(
    bool Success,
    string FilePath,
    string? ActualSha256 = null,
    string? Error = null);
