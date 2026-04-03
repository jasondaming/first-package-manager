using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Tests;

public class DependencyResolverTests
{
    [Fact]
    public async Task Resolve_SimpleDependencyChain_ReturnsCorrectOrder()
    {
        // A depends on B, B depends on C -> install order: C, B, A
        var registry = new FakeRegistryClient(new Dictionary<string, PackageManifest>
        {
            ["A"] = MakeManifest("A", "1.0.0", new[] { MakeDep("B") }),
            ["B"] = MakeManifest("B", "1.0.0", new[] { MakeDep("C") }),
            ["C"] = MakeManifest("C", "1.0.0")
        });

        var resolver = new DependencyResolver();
        var result = await resolver.ResolveAsync(new[] { "A" }, "windows-x64", registry);

        Assert.Equal(3, result.Count);
        Assert.Equal("C", result[0].Id);
        Assert.Equal("B", result[1].Id);
        Assert.Equal("A", result[2].Id);
    }

    [Fact]
    public async Task Resolve_CircularDependency_Throws()
    {
        // A -> B -> C -> A
        var registry = new FakeRegistryClient(new Dictionary<string, PackageManifest>
        {
            ["A"] = MakeManifest("A", "1.0.0", new[] { MakeDep("B") }),
            ["B"] = MakeManifest("B", "1.0.0", new[] { MakeDep("C") }),
            ["C"] = MakeManifest("C", "1.0.0", new[] { MakeDep("A") })
        });

        var resolver = new DependencyResolver();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(new[] { "A" }, "windows-x64", registry));

        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact]
    public async Task Resolve_ConflictDetection_Throws()
    {
        // A conflicts with B, both requested
        var registry = new FakeRegistryClient(new Dictionary<string, PackageManifest>
        {
            ["A"] = MakeManifest("A", "1.0.0", conflicts: new[] { "B" }),
            ["B"] = MakeManifest("B", "1.0.0")
        });

        var resolver = new DependencyResolver();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(new[] { "A", "B" }, "windows-x64", registry));

        Assert.Contains("conflicts", ex.Message);
    }

    [Fact]
    public async Task Resolve_PlatformFiltering_ThrowsWhenNoPlatformArtifact()
    {
        var manifest = MakeManifest("A", "1.0.0");
        manifest.Artifacts["linux-x64"] = new PackageArtifact
        {
            Url = "https://example.com/a-linux.zip",
            Size = 100
        };

        var registry = new FakeRegistryClient(new Dictionary<string, PackageManifest>
        {
            ["A"] = manifest
        });

        var resolver = new DependencyResolver();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(new[] { "A" }, "windows-x64", registry));

        Assert.Contains("no artifact for platform", ex.Message);
    }

    [Fact]
    public async Task Resolve_TransitiveDependencies_ResolvedCorrectly()
    {
        // A -> B, A -> C, B -> D, C -> D -> install order: D, B, C, A (or D, C, B, A)
        var registry = new FakeRegistryClient(new Dictionary<string, PackageManifest>
        {
            ["A"] = MakeManifest("A", "1.0.0", new[] { MakeDep("B"), MakeDep("C") }),
            ["B"] = MakeManifest("B", "1.0.0", new[] { MakeDep("D") }),
            ["C"] = MakeManifest("C", "1.0.0", new[] { MakeDep("D") }),
            ["D"] = MakeManifest("D", "1.0.0")
        });

        var resolver = new DependencyResolver();
        var result = await resolver.ResolveAsync(new[] { "A" }, "windows-x64", registry);

        Assert.Equal(4, result.Count);
        // D must come before B and C; B and C must come before A
        var dIndex = result.ToList().FindIndex(m => m.Id == "D");
        var bIndex = result.ToList().FindIndex(m => m.Id == "B");
        var cIndex = result.ToList().FindIndex(m => m.Id == "C");
        var aIndex = result.ToList().FindIndex(m => m.Id == "A");

        Assert.True(dIndex < bIndex, "D should be installed before B");
        Assert.True(dIndex < cIndex, "D should be installed before C");
        Assert.True(bIndex < aIndex, "B should be installed before A");
        Assert.True(cIndex < aIndex, "C should be installed before A");
    }

    [Fact]
    public async Task Resolve_NoDependencies_ReturnsSinglePackage()
    {
        var registry = new FakeRegistryClient(new Dictionary<string, PackageManifest>
        {
            ["A"] = MakeManifest("A", "1.0.0")
        });

        var resolver = new DependencyResolver();
        var result = await resolver.ResolveAsync(new[] { "A" }, "windows-x64", registry);

        Assert.Single(result);
        Assert.Equal("A", result[0].Id);
    }

    private static PackageManifest MakeManifest(
        string id,
        string version,
        PackageDependency[]? deps = null,
        string[]? conflicts = null)
    {
        return new PackageManifest
        {
            Id = id,
            Name = id,
            Version = version,
            Dependencies = deps?.ToList() ?? [],
            Conflicts = conflicts?.ToList() ?? [],
            // No artifacts means no platform filtering needed
            Artifacts = new Dictionary<string, PackageArtifact>()
        };
    }

    private static PackageDependency MakeDep(string id, string? versionRange = null)
    {
        return new PackageDependency
        {
            Id = id,
            VersionRange = versionRange,
            Type = DependencyType.Required
        };
    }
}

internal class FakeRegistryClient : IRegistryClient
{
    private readonly Dictionary<string, PackageManifest> _manifests;
    private readonly RegistryIndex _index;
    private readonly Dictionary<string, BundleDefinition> _bundles;

    public FakeRegistryClient(
        Dictionary<string, PackageManifest> manifests,
        RegistryIndex? index = null,
        Dictionary<string, BundleDefinition>? bundles = null)
    {
        _manifests = manifests;
        _index = index ?? new RegistryIndex
        {
            Packages = manifests.Values.Select(m => new PackageSummary
            {
                Id = m.Id,
                Name = m.Name,
                Version = m.Version,
                Season = m.Season
            }).ToList()
        };
        _bundles = bundles ?? new Dictionary<string, BundleDefinition>();
    }

    public bool IsOffline => false;

    public Task<RegistryIndex> FetchRegistryAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        return Task.FromResult(_index);
    }

    public Task<PackageManifest> GetPackageAsync(string packageId, CancellationToken ct = default)
    {
        if (_manifests.TryGetValue(packageId, out var manifest))
        {
            return Task.FromResult(manifest);
        }
        throw new KeyNotFoundException($"Package '{packageId}' not found.");
    }

    public Task<IReadOnlyList<PackageSummary>> SearchAsync(
        string? query = null,
        CompetitionProgram? program = null,
        int? year = null,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<PackageSummary>>(_index.Packages);
    }

    public Task<BundleDefinition> GetBundleAsync(string bundleId, CancellationToken ct = default)
    {
        if (_bundles.TryGetValue(bundleId, out var bundle))
        {
            return Task.FromResult(bundle);
        }
        throw new KeyNotFoundException($"Bundle '{bundleId}' not found.");
    }
}
