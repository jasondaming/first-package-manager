using System.Net;
using System.Text.Json;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Tests;

public class RegistryClientTests : IDisposable
{
    private readonly string _tempDir;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public RegistryClientTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "frctoolsuite-reg-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static RegistryIndex CreateSampleIndex()
    {
        return new RegistryIndex
        {
            SchemaVersion = "1.0",
            LastUpdated = DateTimeOffset.UtcNow,
            Seasons = [new SeasonInfo { Year = 2026, Status = "active" }],
            Publishers = [new PublisherInfo { Id = "wpilib", Name = "WPILib", Trusted = true }],
            Packages =
            [
                new PackageSummary
                {
                    Id = "wpilib.jdk",
                    Name = "JDK 17",
                    Season = 2026,
                    Version = "17.0.16",
                    Category = "runtime",
                    Competition = CompetitionProgram.Frc,
                    PublisherId = "wpilib",
                    Tags = ["java", "jdk"],
                    Description = "Eclipse Adoptium JDK 17.",
                    ManifestUrl = "https://example.com/packages/wpilib/jdk.json",
                },
                new PackageSummary
                {
                    Id = "wpilib.vscode",
                    Name = "VS Code",
                    Season = 2026,
                    Version = "1.96.0",
                    Category = "ide",
                    Competition = CompetitionProgram.Frc,
                    PublisherId = "wpilib",
                    Tags = ["ide", "vscode"],
                    Description = "Visual Studio Code editor.",
                    ManifestUrl = "https://example.com/packages/wpilib/vscode.json",
                },
                new PackageSummary
                {
                    Id = "ftc.sdk",
                    Name = "FTC SDK",
                    Season = 2025,
                    Version = "10.1.0",
                    Category = "sdk",
                    Competition = CompetitionProgram.Ftc,
                    PublisherId = "ftc",
                    Tags = ["ftc", "sdk"],
                    Description = "FTC Software Development Kit.",
                    ManifestUrl = "https://example.com/packages/ftc/sdk.json",
                },
            ],
        };
    }

    [Fact]
    public async Task OfflineFallback_UsesCachedIndex()
    {
        // Pre-populate cache
        var cacheDir = Path.Combine(_tempDir, "cache");
        Directory.CreateDirectory(cacheDir);
        var indexPath = Path.Combine(cacheDir, "registry-index.json");
        var index = CreateSampleIndex();
        var json = JsonSerializer.Serialize(index, JsonOptions);
        await File.WriteAllTextAsync(indexPath, json);

        // Create client with handler that always fails
        var handler = new FakeHttpMessageHandler((_, _) =>
        {
            throw new HttpRequestException("Simulated offline");
        });
        var httpClient = new HttpClient(handler);
        var client = new RegistryClient(httpClient, "https://example.com/index.json", cacheDir);

        var result = await client.FetchRegistryAsync();

        Assert.True(client.IsOffline);
        Assert.Equal(3, result.Packages.Count);
        Assert.Equal("wpilib.jdk", result.Packages[0].Id);
    }

    [Fact]
    public async Task SearchAsync_FiltersByQuery()
    {
        var cacheDir = Path.Combine(_tempDir, "cache-search");
        var handler = CreateHandlerWithIndex(CreateSampleIndex());
        var httpClient = new HttpClient(handler);
        var client = new RegistryClient(httpClient, "https://example.com/index.json", cacheDir);

        var results = await client.SearchAsync(query: "JDK");

        Assert.Single(results);
        Assert.Equal("wpilib.jdk", results[0].Id);
    }

    [Fact]
    public async Task SearchAsync_FiltersByProgramAndYear()
    {
        var cacheDir = Path.Combine(_tempDir, "cache-filter");
        var handler = CreateHandlerWithIndex(CreateSampleIndex());
        var httpClient = new HttpClient(handler);
        var client = new RegistryClient(httpClient, "https://example.com/index.json", cacheDir);

        var frcResults = await client.SearchAsync(program: CompetitionProgram.Frc, year: 2026);
        Assert.Equal(2, frcResults.Count);
        Assert.All(frcResults, p => Assert.Equal(CompetitionProgram.Frc, p.Competition));

        var ftcResults = await client.SearchAsync(program: CompetitionProgram.Ftc);
        Assert.Single(ftcResults);
        Assert.Equal("ftc.sdk", ftcResults[0].Id);
    }

    [Fact]
    public async Task ETagCaching_SendsIfNoneMatchHeader()
    {
        var cacheDir = Path.Combine(_tempDir, "cache-etag");
        Directory.CreateDirectory(cacheDir);
        var index = CreateSampleIndex();
        var indexJson = JsonSerializer.Serialize(index, JsonOptions);

        string? capturedIfNoneMatch = null;
        var callCount = 0;

        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            callCount++;
            capturedIfNoneMatch = request.Headers.IfNoneMatch.FirstOrDefault()?.Tag;

            if (callCount == 1)
            {
                // First call: return full response with ETag
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(indexJson),
                };
                response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"abc123\"");
                return Task.FromResult(response);
            }
            else
            {
                // Second call: return 304 Not Modified
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            }
        });

        var httpClient = new HttpClient(handler);
        var client1 = new RegistryClient(httpClient, "https://example.com/index.json", cacheDir);

        // First fetch -- no etag sent
        var result1 = await client1.FetchRegistryAsync();
        Assert.Null(capturedIfNoneMatch);
        Assert.Equal(3, result1.Packages.Count);

        // Second fetch with a new client instance (no in-memory cache) to trigger conditional request
        var client2 = new RegistryClient(httpClient, "https://example.com/index.json", cacheDir);
        var result2 = await client2.FetchRegistryAsync();
        Assert.Equal("\"abc123\"", capturedIfNoneMatch);
        Assert.Equal(3, result2.Packages.Count);
    }

    private static FakeHttpMessageHandler CreateHandlerWithIndex(RegistryIndex index)
    {
        var json = JsonSerializer.Serialize(index, JsonOptions);
        return new FakeHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json),
            };
            return Task.FromResult(response);
        });
    }
}

internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public FakeHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return _handler(request, cancellationToken);
    }
}
