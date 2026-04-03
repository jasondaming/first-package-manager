using System.Net;
using System.Security.Cryptography;
using FrcToolsuite.Core.Download;

namespace FrcToolsuite.Core.Tests;

public class DownloadManagerTests : IDisposable
{
    private readonly string _tempDir;

    public DownloadManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DownloadManagerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public async Task DownloadAsync_SuccessfulDownload_WithCorrectSha256()
    {
        var content = "Hello, FRC World!"u8.ToArray();
        var sha256 = ComputeSha256(content);
        var targetPath = Path.Combine(_tempDir, "hello.txt");

        var handler = new FakeHttpMessageHandler();
        handler.AddResponse("https://example.com/hello.txt", HttpStatusCode.OK, content);

        using var http = new HttpClient(handler);
        var manager = new DownloadManager(http);

        var request = new DownloadRequest(
            "https://example.com/hello.txt",
            targetPath,
            ExpectedSha256: sha256);

        var result = await manager.DownloadAsync(request);

        Assert.True(result.Success);
        Assert.Equal(targetPath, result.FilePath);
        Assert.Equal(sha256, result.ActualSha256);
        Assert.Null(result.Error);
        Assert.True(File.Exists(targetPath));
        Assert.Equal(content, await File.ReadAllBytesAsync(targetPath));
    }

    [Fact]
    public async Task DownloadAsync_Sha256Mismatch_ReturnsFalse()
    {
        var content = "actual content"u8.ToArray();
        var targetPath = Path.Combine(_tempDir, "bad_hash.txt");

        var handler = new FakeHttpMessageHandler();
        handler.AddResponse("https://example.com/file.bin", HttpStatusCode.OK, content);

        using var http = new HttpClient(handler);
        var manager = new DownloadManager(http);

        var request = new DownloadRequest(
            "https://example.com/file.bin",
            targetPath,
            ExpectedSha256: "0000000000000000000000000000000000000000000000000000000000000000");

        var result = await manager.DownloadAsync(request);

        Assert.False(result.Success);
        Assert.Contains("SHA-256 mismatch", result.Error);
        Assert.NotNull(result.ActualSha256);
        Assert.False(File.Exists(targetPath));
    }

    [Fact]
    public async Task DownloadAsync_MirrorFallback_PrimaryFails_MirrorSucceeds()
    {
        var content = "mirror content"u8.ToArray();
        var sha256 = ComputeSha256(content);
        var targetPath = Path.Combine(_tempDir, "mirror.txt");

        var handler = new FakeHttpMessageHandler();
        handler.AddResponse("https://primary.example.com/file.bin",
            HttpStatusCode.InternalServerError, Array.Empty<byte>());
        handler.AddResponse("https://mirror1.example.com/file.bin",
            HttpStatusCode.OK, content);

        using var http = new HttpClient(handler);
        var manager = new DownloadManager(http);

        var request = new DownloadRequest(
            "https://primary.example.com/file.bin",
            targetPath,
            ExpectedSha256: sha256,
            Mirrors: new[] { "https://mirror1.example.com/file.bin" });

        var result = await manager.DownloadAsync(request);

        Assert.True(result.Success);
        Assert.Equal(sha256, result.ActualSha256);
        Assert.True(File.Exists(targetPath));
    }

    [Fact]
    public async Task DownloadParallelAsync_RespectsMaxConcurrency()
    {
        var content = "parallel"u8.ToArray();
        var sha256 = ComputeSha256(content);
        int maxConcurrency = 2;
        int totalRequests = 6;

        var handler = new FakeHttpMessageHandler();
        var concurrencyTracker = new ConcurrencyTracker();
        handler.OnBeforeSend = () => concurrencyTracker.Enter();
        handler.OnAfterSend = () => concurrencyTracker.Exit();

        for (int i = 0; i < totalRequests; i++)
        {
            handler.AddResponse($"https://example.com/file{i}.bin",
                HttpStatusCode.OK, content);
        }

        using var http = new HttpClient(handler);
        var manager = new DownloadManager(http);

        var requests = Enumerable.Range(0, totalRequests).Select(i =>
            new DownloadRequest(
                $"https://example.com/file{i}.bin",
                Path.Combine(_tempDir, $"file{i}.bin"),
                ExpectedSha256: sha256)).ToList();

        var results = await manager.DownloadParallelAsync(requests, maxConcurrency);

        Assert.Equal(totalRequests, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
        Assert.True(concurrencyTracker.MaxObserved <= maxConcurrency,
            $"Max concurrency was {concurrencyTracker.MaxObserved}, expected at most {maxConcurrency}");
    }

    [Fact]
    public async Task DownloadAsync_ProgressReporting_ReportsCorrectByteCounts()
    {
        var content = new byte[1024 * 100]; // 100KB
        Random.Shared.NextBytes(content);
        var sha256 = ComputeSha256(content);
        var targetPath = Path.Combine(_tempDir, "progress.bin");

        var handler = new FakeHttpMessageHandler();
        handler.AddResponse("https://example.com/large.bin", HttpStatusCode.OK, content);

        using var http = new HttpClient(handler);
        var manager = new DownloadManager(http);

        var progressReports = new List<DownloadProgress>();
        var progress = new Progress<DownloadProgress>(p => progressReports.Add(p));

        var request = new DownloadRequest(
            "https://example.com/large.bin",
            targetPath,
            ExpectedSha256: sha256,
            ExpectedSize: content.Length);

        var result = await manager.DownloadAsync(request, progress);

        // Allow progress callbacks to be delivered (they are posted to the sync context)
        await Task.Delay(500);

        Assert.True(result.Success);
        Assert.NotEmpty(progressReports);

        // Last progress report should have all bytes
        var lastReport = progressReports[^1];
        Assert.Equal(content.Length, lastReport.BytesDownloaded);
        Assert.Equal("progress.bin", lastReport.CurrentFile);
    }

    [Fact]
    public async Task DownloadAsync_ResumeFromPartialFile()
    {
        // Simulate a partial download: first 10 bytes already on disk
        var fullContent = "This is the complete file content for resume testing."u8.ToArray();
        var partialContent = fullContent[..10];
        var remainingContent = fullContent[10..];
        var sha256 = ComputeSha256(fullContent);
        var targetPath = Path.Combine(_tempDir, "resume.txt");
        var tmpPath = targetPath + ".tmp";

        // Write partial content to .tmp file
        await File.WriteAllBytesAsync(tmpPath, partialContent);

        var handler = new FakeHttpMessageHandler();
        // Server responds with 206 Partial Content for Range requests
        handler.AddRangeResponse("https://example.com/resume.bin",
            fullContent);

        using var http = new HttpClient(handler);
        var manager = new DownloadManager(http);

        var request = new DownloadRequest(
            "https://example.com/resume.bin",
            targetPath,
            ExpectedSha256: sha256);

        var result = await manager.DownloadAsync(request);

        Assert.True(result.Success);
        Assert.Equal(sha256, result.ActualSha256);
        Assert.True(File.Exists(targetPath));
        Assert.Equal(fullContent, await File.ReadAllBytesAsync(targetPath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public async Task DownloadAsync_NoExpectedHash_SucceedsWithAnyContent(int seed)
    {
        var content = new byte[64];
        new Random(seed).NextBytes(content);
        var targetPath = Path.Combine(_tempDir, $"nohash_{seed}.bin");

        var handler = new FakeHttpMessageHandler();
        handler.AddResponse($"https://example.com/file_{seed}.bin", HttpStatusCode.OK, content);

        using var http = new HttpClient(handler);
        var manager = new DownloadManager(http);

        var request = new DownloadRequest(
            $"https://example.com/file_{seed}.bin",
            targetPath);

        var result = await manager.DownloadAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.ActualSha256);
    }

    private static string ComputeSha256(byte[] data)
    {
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }

    private sealed class ConcurrencyTracker
    {
        private int _current;
        private int _maxObserved;

        public int MaxObserved => _maxObserved;

        public void Enter()
        {
            var val = Interlocked.Increment(ref _current);
            // Update max using a lock-free CAS loop
            int observed;
            do
            {
                observed = Volatile.Read(ref _maxObserved);
                if (val <= observed) break;
            }
            while (Interlocked.CompareExchange(ref _maxObserved, val, observed) != observed);
        }

        public void Exit()
        {
            Interlocked.Decrement(ref _current);
        }
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _handlers = new();

        public Action? OnBeforeSend { get; set; }
        public Action? OnAfterSend { get; set; }

        public void AddResponse(string url, HttpStatusCode statusCode, byte[] content)
        {
            _handlers[url] = _ =>
            {
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new ByteArrayContent(content)
                };
                response.Content.Headers.ContentLength = content.Length;
                return response;
            };
        }

        public void AddRangeResponse(string url, byte[] fullContent)
        {
            _handlers[url] = request =>
            {
                if (request.Headers.Range?.Ranges.FirstOrDefault() is { } range
                    && range.From.HasValue)
                {
                    long from = range.From.Value;
                    var slice = fullContent[(int)from..];
                    var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
                    {
                        Content = new ByteArrayContent(slice)
                    };
                    response.Content.Headers.ContentLength = slice.Length;
                    return response;
                }

                var fullResponse = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(fullContent)
                };
                fullResponse.Content.Headers.ContentLength = fullContent.Length;
                return fullResponse;
            };
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            OnBeforeSend?.Invoke();
            try
            {
                // Small delay to make concurrency observable
                await Task.Delay(10, cancellationToken);

                var url = request.RequestUri?.ToString() ?? string.Empty;
                if (_handlers.TryGetValue(url, out var factory))
                {
                    return factory(request);
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            finally
            {
                OnAfterSend?.Invoke();
            }
        }
    }
}
