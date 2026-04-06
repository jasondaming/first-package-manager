using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FrcToolsuite.Core.Registry;

public class RegistryClient : IRegistryClient
{
    private static readonly string DefaultCacheDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".frctoolsuite",
            "cache");

    private static readonly string[] DefaultRegistryUrls = new[]
    {
        "https://frcmaven.wpi.edu/ui/native/vendordeps/installer-index.json",  // WPI hosted (preferred)
        "https://raw.githubusercontent.com/jasondaming/vendor-json-repo/main/installer-index.json",  // GitHub fallback
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly string[] _registryUrls;
    private readonly string _cacheDir;
    private readonly string _indexCachePath;
    private readonly string _etagCachePath;
    private readonly string _packagesCacheDir;

    private RegistryIndex? _cachedIndex;
    private string? _lastSuccessfulBaseUrl;

    public bool IsOffline { get; private set; }

    public RegistryClient(HttpClient httpClient, string? registryUrl = null, string? cacheDir = null)
        : this(httpClient, registryUrl != null ? new[] { registryUrl } : null, cacheDir)
    {
    }

    public RegistryClient(HttpClient httpClient, string[]? registryUrls, string? cacheDir)
    {
        _httpClient = httpClient;
        _registryUrls = registryUrls ?? DefaultRegistryUrls;
        _cacheDir = cacheDir ?? DefaultCacheDir;
        _indexCachePath = Path.Combine(_cacheDir, "registry-index.json");
        _etagCachePath = Path.Combine(_cacheDir, "registry-etag.txt");
        _packagesCacheDir = Path.Combine(_cacheDir, "packages");
    }

    /// <summary>
    /// Orders registry URLs so the last successful base URL is tried first.
    /// </summary>
    private IEnumerable<string> GetOrderedUrls()
    {
        if (_lastSuccessfulBaseUrl is not null)
        {
            var preferred = _registryUrls
                .FirstOrDefault(u => GetBaseUrl(u) == _lastSuccessfulBaseUrl);
            if (preferred is not null)
            {
                yield return preferred;
                foreach (var url in _registryUrls)
                {
                    if (url != preferred)
                    {
                        yield return url;
                    }
                }

                yield break;
            }
        }

        foreach (var url in _registryUrls)
        {
            yield return url;
        }
    }

    private static string GetBaseUrl(string registryUrl)
    {
        var lastSlash = registryUrl.LastIndexOf('/');
        return lastSlash >= 0 ? registryUrl[..lastSlash] : registryUrl;
    }

    public async Task<RegistryIndex> FetchRegistryAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh && _cachedIndex is not null)
        {
            return _cachedIndex;
        }

        EnsureCacheDirectory();

        HttpRequestException? lastException = null;

        foreach (var registryUrl in GetOrderedUrls())
        {
            try
            {
                var result = await TryFetchRegistryFromUrlAsync(registryUrl, forceRefresh, ct)
                    .ConfigureAwait(false);
                if (result is not null)
                {
                    _lastSuccessfulBaseUrl = GetBaseUrl(registryUrl);
                    return result;
                }
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                // Try next URL
            }
        }

        // All URLs failed -- fall back to local cache
        IsOffline = true;

        var cached = await LoadCachedIndexAsync(ct).ConfigureAwait(false);
        if (cached is not null)
        {
            _cachedIndex = cached;
            return _cachedIndex;
        }

        throw lastException ?? new HttpRequestException("All registry URLs failed and no cached index is available.");
    }

    private async Task<RegistryIndex?> TryFetchRegistryFromUrlAsync(string registryUrl, bool forceRefresh, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, registryUrl);

        if (!forceRefresh && File.Exists(_etagCachePath))
        {
            var etag = await File.ReadAllTextAsync(_etagCachePath, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(etag))
            {
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
            }
        }

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            IsOffline = false;
            _cachedIndex = await LoadCachedIndexAsync(ct).ConfigureAwait(false);
            if (_cachedIndex is not null)
            {
                return _cachedIndex;
            }
        }

        response.EnsureSuccessStatusCode();
        IsOffline = false;

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        _cachedIndex = JsonSerializer.Deserialize<RegistryIndex>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize registry index.");

        await File.WriteAllTextAsync(_indexCachePath, json, ct).ConfigureAwait(false);

        if (response.Headers.ETag is not null)
        {
            await File.WriteAllTextAsync(_etagCachePath, response.Headers.ETag.ToString(), ct)
                .ConfigureAwait(false);
        }

        return _cachedIndex;
    }

    public async Task<PackageManifest> GetPackageAsync(string packageId, CancellationToken ct = default)
    {
        EnsureCacheDirectory();
        Directory.CreateDirectory(_packagesCacheDir);

        var sanitizedId = packageId.Replace("/", "_").Replace("\\", "_");
        var cachedPath = Path.Combine(_packagesCacheDir, $"{sanitizedId}.json");

        var index = await FetchRegistryAsync(ct: ct).ConfigureAwait(false);
        var summary = index.Packages.FirstOrDefault(
            p => string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Package '{packageId}' not found in registry index.");

        // Build candidate URLs: the manifest URL as-is, plus rebased on each registry base URL
        var candidateUrls = BuildCandidateUrls(summary.ManifestUrl);

        HttpRequestException? lastException = null;

        foreach (var url in candidateUrls)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var manifest = JsonSerializer.Deserialize<PackageManifest>(json, JsonOptions)
                    ?? throw new InvalidOperationException($"Failed to deserialize manifest for '{packageId}'.");

                await File.WriteAllTextAsync(cachedPath, json, ct).ConfigureAwait(false);
                return manifest;
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
            }
        }

        // All URLs failed -- fall back to cache
        IsOffline = true;

        if (File.Exists(cachedPath))
        {
            var json = await File.ReadAllTextAsync(cachedPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<PackageManifest>(json, JsonOptions)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize cached manifest for '{packageId}'.");
        }

        throw lastException ?? new HttpRequestException($"Failed to fetch manifest for '{packageId}'.");
    }

    public async Task<IReadOnlyList<PackageSummary>> SearchAsync(
        string? query = null,
        CompetitionProgram? program = null,
        int? year = null,
        CancellationToken ct = default)
    {
        var index = await FetchRegistryAsync(ct: ct).ConfigureAwait(false);

        IEnumerable<PackageSummary> results = index.Packages;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            results = results.Where(p =>
                p.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || p.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
                || p.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        if (program.HasValue)
        {
            results = results.Where(p => p.Competition == program.Value);
        }

        if (year.HasValue)
        {
            results = results.Where(p => p.Season == year.Value);
        }

        return results.ToList().AsReadOnly();
    }

    public async Task<BundleDefinition> GetBundleAsync(string bundleId, CancellationToken ct = default)
    {
        var index = await FetchRegistryAsync(ct: ct).ConfigureAwait(false);
        _ = index; // Ensures registry is loaded; bundles have their own URL pattern.

        // Build candidate bundle URLs from all registry base URLs
        var candidateUrls = new List<string>();
        if (_lastSuccessfulBaseUrl is not null)
        {
            candidateUrls.Add($"{_lastSuccessfulBaseUrl}/bundles/{bundleId}.json");
        }

        foreach (var registryUrl in _registryUrls)
        {
            var bundleUrl = $"{GetBaseUrl(registryUrl)}/bundles/{bundleId}.json";
            if (!candidateUrls.Contains(bundleUrl))
            {
                candidateUrls.Add(bundleUrl);
            }
        }

        HttpRequestException? lastException = null;

        foreach (var bundleUrl in candidateUrls)
        {
            try
            {
                using var response = await _httpClient.GetAsync(bundleUrl, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<BundleDefinition>(json, JsonOptions)
                    ?? throw new InvalidOperationException($"Failed to deserialize bundle '{bundleId}'.");
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
            }
        }

        // All URLs failed -- fall back to cache
        IsOffline = true;

        var localPath = Path.Combine(_cacheDir, "bundles", $"{bundleId}.json");
        if (File.Exists(localPath))
        {
            var json = await File.ReadAllTextAsync(localPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<BundleDefinition>(json, JsonOptions)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize cached bundle '{bundleId}'.");
        }

        throw lastException ?? new HttpRequestException($"Failed to fetch bundle '{bundleId}'.");
    }

    /// <summary>
    /// Builds a list of candidate URLs for a resource, trying the last successful base URL first,
    /// then the original URL, then rebased on all other registry base URLs.
    /// </summary>
    private List<string> BuildCandidateUrls(string originalUrl)
    {
        var candidates = new List<string>();

        // If we have a last successful base URL, try rebasing the resource path onto it first
        if (_lastSuccessfulBaseUrl is not null)
        {
            var uri = new Uri(originalUrl);
            var rebased = $"{_lastSuccessfulBaseUrl}{uri.AbsolutePath}";
            if (!candidates.Contains(rebased))
            {
                candidates.Add(rebased);
            }
        }

        // Always try the original URL from the manifest
        if (!candidates.Contains(originalUrl))
        {
            candidates.Add(originalUrl);
        }

        // Also try rebasing onto each registry base URL
        foreach (var registryUrl in _registryUrls)
        {
            var baseUrl = GetBaseUrl(registryUrl);
            var uri = new Uri(originalUrl);
            var rebased = $"{baseUrl}{uri.AbsolutePath}";
            if (!candidates.Contains(rebased))
            {
                candidates.Add(rebased);
            }
        }

        return candidates;
    }

    private async Task<RegistryIndex?> LoadCachedIndexAsync(CancellationToken ct)
    {
        if (!File.Exists(_indexCachePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_indexCachePath, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<RegistryIndex>(json, JsonOptions);
    }

    private void EnsureCacheDirectory()
    {
        if (!Directory.Exists(_cacheDir))
        {
            Directory.CreateDirectory(_cacheDir);
        }
    }
}
