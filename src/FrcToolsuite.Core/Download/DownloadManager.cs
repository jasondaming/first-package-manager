using System.Security.Cryptography;

namespace FrcToolsuite.Core.Download;

public sealed class DownloadManager : IDownloadManager
{
    private readonly HttpClient _http;

    public DownloadManager(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var urls = BuildUrlList(request);

        foreach (var url in urls)
        {
            var result = await TryDownloadFromUrlAsync(url, request, progress, ct)
                .ConfigureAwait(false);

            if (result.Success)
            {
                return result;
            }

            // If this was the last URL, return the failure result
            if (url == urls[^1])
            {
                return result;
            }
        }

        // Unreachable, but satisfies the compiler
        return new DownloadResult(false, request.TargetPath, Error: "No URLs available");
    }

    public async Task<IReadOnlyList<DownloadResult>> DownloadParallelAsync(
        IReadOnlyList<DownloadRequest> requests,
        int maxConcurrency = 4,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = requests.Select(async request =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await DownloadAsync(request, progress, ct).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static string[] BuildUrlList(DownloadRequest request)
    {
        if (request.Mirrors is { Length: > 0 })
        {
            var list = new string[1 + request.Mirrors.Length];
            list[0] = request.Url;
            request.Mirrors.CopyTo(list, 1);
            return list;
        }

        return [request.Url];
    }

    private async Task<DownloadResult> TryDownloadFromUrlAsync(
        string url,
        DownloadRequest request,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var tmpPath = request.TargetPath + ".tmp";
        try
        {
            var targetDir = Path.GetDirectoryName(request.TargetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            long existingLength = 0;
            if (File.Exists(tmpPath))
            {
                existingLength = new FileInfo(tmpPath).Length;
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            if (existingLength > 0)
            {
                httpRequest.Headers.Range =
                    new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);
            }

            using var response = await _http
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new DownloadResult(false, request.TargetPath,
                    Error: $"HTTP {(int)response.StatusCode} from {url}");
            }

            bool isResume = response.StatusCode == System.Net.HttpStatusCode.PartialContent
                            && existingLength > 0;

            long totalBytes = response.Content.Headers.ContentLength ?? request.ExpectedSize ?? -1;
            if (isResume && totalBytes > 0)
            {
                totalBytes += existingLength;
            }
            else if (!isResume && totalBytes <= 0 && request.ExpectedSize.HasValue)
            {
                totalBytes = request.ExpectedSize.Value;
            }

            // If the server didn't honor our Range request, start from scratch
            if (!isResume && existingLength > 0)
            {
                existingLength = 0;
            }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            // If resuming, we need to hash the existing partial data first
            if (isResume && existingLength > 0)
            {
                await HashExistingFileAsync(tmpPath, hash, ct).ConfigureAwait(false);
            }

            var fileMode = isResume ? FileMode.Append : FileMode.Create;
            long bytesDownloaded = existingLength;
            var fileName = Path.GetFileName(request.TargetPath);

            await using (var contentStream = await response.Content.ReadAsStreamAsync(ct)
                .ConfigureAwait(false))
            await using (var fileStream = new FileStream(tmpPath, fileMode, FileAccess.Write,
                FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)
                    .ConfigureAwait(false)) > 0)
                {
                    hash.AppendData(buffer, 0, bytesRead);
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct)
                        .ConfigureAwait(false);

                    bytesDownloaded += bytesRead;
                    progress?.Report(new DownloadProgress(bytesDownloaded, totalBytes, fileName));
                }
            }

            var hashBytes = hash.GetHashAndReset();
            var actualSha256 = Convert.ToHexString(hashBytes).ToLowerInvariant();

            if (request.ExpectedSha256 is not null
                && !string.Equals(actualSha256, request.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(tmpPath);
                return new DownloadResult(false, request.TargetPath, actualSha256,
                    Error: $"SHA-256 mismatch: expected {request.ExpectedSha256}, got {actualSha256}");
            }

            // Rename temp file to final target
            if (File.Exists(request.TargetPath))
            {
                File.Delete(request.TargetPath);
            }

            File.Move(tmpPath, request.TargetPath);

            return new DownloadResult(true, request.TargetPath, actualSha256);
        }
        catch (HttpRequestException ex)
        {
            TryDelete(tmpPath);
            return new DownloadResult(false, request.TargetPath,
                Error: $"Request failed for {url}: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return new DownloadResult(false, request.TargetPath, Error: "Download cancelled");
        }
        catch (Exception ex)
        {
            TryDelete(tmpPath);
            return new DownloadResult(false, request.TargetPath,
                Error: $"Unexpected error: {ex.Message}");
        }
    }

    private static async Task HashExistingFileAsync(
        string path, IncrementalHash hash, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, useAsync: true);
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await fs.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            hash.AppendData(buffer, 0, bytesRead);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
