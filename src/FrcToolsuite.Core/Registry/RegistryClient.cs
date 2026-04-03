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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly string _registryUrl;
    private readonly string _cacheDir;
    private readonly string _indexCachePath;
    private readonly string _etagCachePath;
    private readonly string _packagesCacheDir;

    private RegistryIndex? _cachedIndex;

    public bool IsOffline { get; private set; }

    public RegistryClient(HttpClient httpClient, string? registryUrl = null, string? cacheDir = null)
    {
        _httpClient = httpClient;
        _registryUrl = registryUrl
            ?? "https://raw.githubusercontent.com/jasondaming/vendor-json-repo/main/installer-index.json";
        _cacheDir = cacheDir ?? DefaultCacheDir;
        _indexCachePath = Path.Combine(_cacheDir, "registry-index.json");
        _etagCachePath = Path.Combine(_cacheDir, "registry-etag.txt");
        _packagesCacheDir = Path.Combine(_cacheDir, "packages");
    }

    public async Task<RegistryIndex> FetchRegistryAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh && _cachedIndex is not null)
        {
            return _cachedIndex;
        }

        EnsureCacheDirectory();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _registryUrl);

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
        catch (HttpRequestException)
        {
            IsOffline = true;

            var cached = await LoadCachedIndexAsync(ct).ConfigureAwait(false);
            if (cached is not null)
            {
                _cachedIndex = cached;
                return _cachedIndex;
            }

            throw;
        }
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

        try
        {
            using var response = await _httpClient.GetAsync(summary.ManifestUrl, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<PackageManifest>(json, JsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize manifest for '{packageId}'.");

            await File.WriteAllTextAsync(cachedPath, json, ct).ConfigureAwait(false);
            return manifest;
        }
        catch (HttpRequestException)
        {
            IsOffline = true;

            if (File.Exists(cachedPath))
            {
                var json = await File.ReadAllTextAsync(cachedPath, ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<PackageManifest>(json, JsonOptions)
                    ?? throw new InvalidOperationException(
                        $"Failed to deserialize cached manifest for '{packageId}'.");
            }

            throw;
        }
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

        var bundleUrl = _registryUrl.Replace("index.json", $"bundles/{bundleId}.json");

        try
        {
            using var response = await _httpClient.GetAsync(bundleUrl, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<BundleDefinition>(json, JsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize bundle '{bundleId}'.");
        }
        catch (HttpRequestException)
        {
            IsOffline = true;

            var localPath = Path.Combine(_cacheDir, "bundles", $"{bundleId}.json");
            if (File.Exists(localPath))
            {
                var json = await File.ReadAllTextAsync(localPath, ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<BundleDefinition>(json, JsonOptions)
                    ?? throw new InvalidOperationException(
                        $"Failed to deserialize cached bundle '{bundleId}'.");
            }

            throw;
        }
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
