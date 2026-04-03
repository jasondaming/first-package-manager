using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Packages;

public class DependencyResolver
{
    private readonly bool _includeRecommended;

    public DependencyResolver(bool includeRecommended = false)
    {
        _includeRecommended = includeRecommended;
    }

    public async Task<IReadOnlyList<PackageManifest>> ResolveAsync(
        IEnumerable<string> requestedPackageIds,
        string platform,
        IRegistryClient registry,
        CancellationToken ct = default)
    {
        // Fetch all manifests for requested packages and their transitive dependencies
        var manifests = new Dictionary<string, PackageManifest>(StringComparer.OrdinalIgnoreCase);
        var toFetch = new Queue<string>(requestedPackageIds);
        var fetched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (toFetch.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var id = toFetch.Dequeue();
            if (!fetched.Add(id))
            {
                continue;
            }

            var manifest = await registry.GetPackageAsync(id, ct);
            manifests[id] = manifest;

            foreach (var dep in manifest.Dependencies)
            {
                if (dep.Type == DependencyType.Required ||
                    (_includeRecommended && dep.Type == DependencyType.Recommended))
                {
                    if (!fetched.Contains(dep.Id))
                    {
                        toFetch.Enqueue(dep.Id);
                    }
                }
            }
        }

        // Check conflicts
        CheckConflicts(manifests);

        // Validate version constraints
        ValidateVersionConstraints(manifests);

        // Validate platform artifacts exist
        ValidatePlatformArtifacts(manifests, platform);

        // Topological sort via DFS
        var sorted = TopologicalSort(manifests);

        return sorted;
    }

    private static void CheckConflicts(Dictionary<string, PackageManifest> manifests)
    {
        foreach (var (id, manifest) in manifests)
        {
            foreach (var conflict in manifest.Conflicts)
            {
                if (manifests.ContainsKey(conflict))
                {
                    throw new InvalidOperationException(
                        $"Package '{id}' conflicts with package '{conflict}'. Both cannot be installed together.");
                }
            }
        }
    }

    private static void ValidateVersionConstraints(Dictionary<string, PackageManifest> manifests)
    {
        foreach (var (_, manifest) in manifests)
        {
            foreach (var dep in manifest.Dependencies)
            {
                if (dep.Type != DependencyType.Required)
                {
                    continue;
                }

                if (!manifests.TryGetValue(dep.Id, out var depManifest))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(dep.VersionRange))
                {
                    var depVersion = PackageVersion.Parse(depManifest.Version);
                    if (!depVersion.SatisfiesRange(dep.VersionRange))
                    {
                        throw new InvalidOperationException(
                            $"Package '{manifest.Id}' requires '{dep.Id}' version '{dep.VersionRange}', " +
                            $"but version '{depManifest.Version}' was resolved.");
                    }
                }
            }
        }
    }

    private static void ValidatePlatformArtifacts(
        Dictionary<string, PackageManifest> manifests,
        string platform)
    {
        foreach (var (id, manifest) in manifests)
        {
            if (manifest.Artifacts.Count > 0 && !manifest.Artifacts.ContainsKey(platform))
            {
                throw new InvalidOperationException(
                    $"Package '{id}' has no artifact for platform '{platform}'.");
            }
        }
    }

    private List<PackageManifest> TopologicalSort(Dictionary<string, PackageManifest> manifests)
    {
        var result = new List<PackageManifest>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in manifests.Keys)
        {
            if (!visited.Contains(id))
            {
                Visit(id, manifests, visited, inStack, result, new List<string>());
            }
        }

        return result;
    }

    private void Visit(
        string id,
        Dictionary<string, PackageManifest> manifests,
        HashSet<string> visited,
        HashSet<string> inStack,
        List<PackageManifest> result,
        List<string> path)
    {
        if (inStack.Contains(id))
        {
            var cycleStart = path.IndexOf(id);
            var cycle = path.Skip(cycleStart).Append(id);
            throw new InvalidOperationException(
                $"Circular dependency detected: {string.Join(" -> ", cycle)}");
        }

        if (visited.Contains(id))
        {
            return;
        }

        inStack.Add(id);
        path.Add(id);

        if (manifests.TryGetValue(id, out var manifest))
        {
            foreach (var dep in manifest.Dependencies)
            {
                if (dep.Type == DependencyType.Required ||
                    (_includeRecommended && dep.Type == DependencyType.Recommended))
                {
                    if (manifests.ContainsKey(dep.Id))
                    {
                        Visit(dep.Id, manifests, visited, inStack, result, path);
                    }
                }
            }

            result.Add(manifest);
        }

        inStack.Remove(id);
        path.RemoveAt(path.Count - 1);
        visited.Add(id);
    }
}
