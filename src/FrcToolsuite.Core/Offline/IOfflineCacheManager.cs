using FrcToolsuite.Core.Packages;

namespace FrcToolsuite.Core.Offline;

public interface IOfflineCacheManager
{
    Task ExportToUsbAsync(
        string targetPath,
        IReadOnlyList<string>? packageIds = null,
        IProgress<OfflineSyncProgress>? progress = null,
        CancellationToken ct = default);

    Task ImportFromUsbAsync(
        string sourcePath,
        IProgress<OfflineSyncProgress>? progress = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetCachedPackageIdsAsync(
        CancellationToken ct = default);

    Task<bool> IsCacheValidAsync(
        string cachePath,
        CancellationToken ct = default);
}

public record OfflineSyncProgress(
    int CompletedItems,
    int TotalItems,
    string CurrentItem,
    long BytesTransferred,
    long TotalBytes);
