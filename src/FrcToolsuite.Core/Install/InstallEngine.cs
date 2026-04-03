using System.Diagnostics;
using System.Text.Json;
using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Platform;
using FrcToolsuite.Core.Registry;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;

namespace FrcToolsuite.Core.Install;

public class InstallEngine : IInstallEngine
{
    private const string ManifestFileName = ".install-manifest.json";

    private readonly IPlatformService _platformService;

    public InstallEngine(IPlatformService platformService)
    {
        _platformService = platformService;
    }

    public async Task<IReadOnlyList<string>> ExtractAsync(
        string archivePath,
        string destinationPath,
        string? archiveType = null,
        CancellationToken ct = default)
    {
        var type = archiveType ?? InferArchiveType(archivePath);
        Directory.CreateDirectory(destinationPath);

        return type switch
        {
            "zip" => await ExtractZipAsync(archivePath, destinationPath, ct),
            "tar.gz" or "tgz" => await ExtractTarGzAsync(archivePath, destinationPath, ct),
            _ => throw new NotSupportedException($"Unsupported archive type: '{type}'")
        };
    }

    public async Task RunPostInstallAsync(
        IReadOnlyList<PostInstallAction> actions,
        string installPath,
        CancellationToken ct = default)
    {
        foreach (var action in actions)
        {
            ct.ThrowIfCancellationRequested();

            // Template variable substitution
            var resolvedParams = new Dictionary<string, string>();
            foreach (var (key, value) in action.Params)
            {
                resolvedParams[key] = value.Replace("{installDir}", installPath);
            }

            switch (action.Action)
            {
                case "set-env-var":
                    if (resolvedParams.TryGetValue("name", out var envName) &&
                        resolvedParams.TryGetValue("value", out var envValue))
                    {
                        _platformService.SetEnvironmentVariable(envName, envValue);
                    }
                    break;

                case "add-to-path":
                    if (resolvedParams.TryGetValue("path", out var pathValue))
                    {
                        _platformService.AddToPath(pathValue);
                    }
                    break;

                case "create-shortcut":
                    if (resolvedParams.TryGetValue("name", out var shortcutName) &&
                        resolvedParams.TryGetValue("target", out var shortcutTarget))
                    {
                        resolvedParams.TryGetValue("icon", out var iconPath);
                        var isDesktop = resolvedParams.TryGetValue("desktop", out var desktopVal) &&
                                        bool.TryParse(desktopVal, out var dv) && dv;
                        _platformService.CreateShortcut(shortcutName, shortcutTarget, iconPath, isDesktop);
                    }
                    break;

                case "run-command":
                    if (resolvedParams.TryGetValue("command", out var command))
                    {
                        resolvedParams.TryGetValue("args", out var args);
                        var psi = new ProcessStartInfo
                        {
                            FileName = command,
                            Arguments = args ?? string.Empty,
                            WorkingDirectory = installPath,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        using var process = Process.Start(psi);
                        if (process != null)
                        {
                            await process.WaitForExitAsync(ct);
                        }
                    }
                    break;
            }
        }
    }

    public async Task RecordInstallAsync(
        InstalledPackage package,
        CancellationToken ct = default)
    {
        var manifest = new InstallManifest
        {
            PackageId = package.PackageId,
            Version = package.Version,
            Season = package.Season.ToString(),
            InstalledAt = package.InstalledAt.UtcDateTime,
            InstallPath = package.InstallPath,
            InstalledFiles = package.InstalledFiles,
            Platform = string.Empty
        };

        var manifestPath = Path.Combine(package.InstallPath, ManifestFileName);
        Directory.CreateDirectory(package.InstallPath);

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json, ct);
    }

    public async Task RemoveInstalledFilesAsync(
        InstalledPackage package,
        CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(package.InstallPath, ManifestFileName);
        string[] files;

        if (File.Exists(manifestPath))
        {
            var json = await File.ReadAllTextAsync(manifestPath, ct);
            var manifest = JsonSerializer.Deserialize<InstallManifest>(json);
            files = manifest?.InstalledFiles ?? [];
        }
        else
        {
            files = package.InstalledFiles;
        }

        // Delete files in reverse order (deepest first), with path traversal protection
        foreach (var file in files.Reverse())
        {
            ct.ThrowIfCancellationRequested();
            var fullPath = SafeJoinPath(package.InstallPath, file);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        // Delete the manifest itself
        if (File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
        }

        // Remove empty directories (bottom-up)
        RemoveEmptyDirectories(package.InstallPath);
    }

    /// <summary>
    /// Safely joins a base directory with a relative path, preventing path traversal attacks.
    /// </summary>
    private static string SafeJoinPath(string baseDir, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
        var normalizedBase = Path.GetFullPath(baseDir + Path.DirectorySeparatorChar);
        if (!fullPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Path traversal detected: '{relativePath}' escapes install directory '{baseDir}'");
        }
        return fullPath;
    }

    private static string InferArchiveType(string path)
    {
        if (path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            return "tar.gz";
        }

        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return "zip";
        }

        return "zip";
    }

    private static Task<IReadOnlyList<string>> ExtractZipAsync(
        string archivePath,
        string destinationPath,
        CancellationToken ct)
    {
        var extractedFiles = new List<string>();

        using var zipFile = new ZipFile(archivePath);
        foreach (ZipEntry entry in zipFile)
        {
            ct.ThrowIfCancellationRequested();

            if (!entry.IsFile)
            {
                continue;
            }

            var entryPath = entry.Name;
            var fullPath = SafeJoinPath(destinationPath, entryPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }

            using var entryStream = zipFile.GetInputStream(entry);
            using var outputStream = File.Create(fullPath);
            entryStream.CopyTo(outputStream);

            extractedFiles.Add(entryPath);
        }

        return Task.FromResult<IReadOnlyList<string>>(extractedFiles);
    }

    private static Task<IReadOnlyList<string>> ExtractTarGzAsync(
        string archivePath,
        string destinationPath,
        CancellationToken ct)
    {
        var extractedFiles = new List<string>();

        using var fileStream = File.OpenRead(archivePath);
        using var gzipStream = new GZipInputStream(fileStream);
        using var tarArchive = TarArchive.CreateInputTarArchive(gzipStream, System.Text.Encoding.UTF8);

        // We need to extract manually to track files
        using var tarInputStream = new TarInputStream(
            new GZipInputStream(File.OpenRead(archivePath)), System.Text.Encoding.UTF8);

        TarEntry? tarEntry;
        while ((tarEntry = tarInputStream.GetNextEntry()) != null)
        {
            ct.ThrowIfCancellationRequested();

            if (tarEntry.IsDirectory)
            {
                continue;
            }

            var entryPath = tarEntry.Name;
            var fullPath = SafeJoinPath(destinationPath, entryPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }

            using var outputStream = File.Create(fullPath);
            tarInputStream.CopyEntryContents(outputStream);

            extractedFiles.Add(entryPath);
        }

        return Task.FromResult<IReadOnlyList<string>>(extractedFiles);
    }

    private static void RemoveEmptyDirectories(string rootDir)
    {
        if (!Directory.Exists(rootDir))
        {
            return;
        }

        foreach (var dir in Directory.GetDirectories(rootDir, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length))
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }

        if (Directory.Exists(rootDir) && !Directory.EnumerateFileSystemEntries(rootDir).Any())
        {
            Directory.Delete(rootDir);
        }
    }
}
