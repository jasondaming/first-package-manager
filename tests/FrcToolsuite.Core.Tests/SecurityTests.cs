using System.IO.Compression;
using FrcToolsuite.Core.Install;
using FrcToolsuite.Core.Platform;

namespace FrcToolsuite.Core.Tests;

public class SecurityTests : IDisposable
{
    private readonly string _tempDir;

    public SecurityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"frc-security-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); }
        catch { /* best effort cleanup */ }
    }

    [Fact]
    public async Task ExtractAsync_ZipSlipPathTraversal_IsRejected()
    {
        // Create a malicious zip with a ../evil.txt entry
        var zipPath = Path.Combine(_tempDir, "malicious.zip");
        var destPath = Path.Combine(_tempDir, "extracted");
        Directory.CreateDirectory(destPath);

        using (var zipStream = File.Create(zipPath))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            // Normal file
            var normalEntry = archive.CreateEntry("safe.txt");
            using (var writer = new StreamWriter(normalEntry.Open()))
            {
                writer.Write("safe content");
            }

            // Malicious path traversal entry
            var evilEntry = archive.CreateEntry("../../../evil.txt");
            using (var writer = new StreamWriter(evilEntry.Open()))
            {
                writer.Write("pwned");
            }
        }

        var engine = new InstallEngine(new StubPlatformService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.ExtractAsync(zipPath, destPath, "zip"));

        Assert.Contains("Path traversal detected", ex.Message);

        // Verify the evil file was NOT created outside the destination
        Assert.False(File.Exists(Path.Combine(_tempDir, "evil.txt")));
    }

    [Fact]
    public async Task ExtractAsync_AbsolutePathEntry_IsRejected()
    {
        var zipPath = Path.Combine(_tempDir, "absolute.zip");
        var destPath = Path.Combine(_tempDir, "extracted2");
        Directory.CreateDirectory(destPath);

        using (var zipStream = File.Create(zipPath))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("/etc/passwd");
            using (var writer = new StreamWriter(entry.Open()))
            {
                writer.Write("root:x:0:0");
            }
        }

        var engine = new InstallEngine(new StubPlatformService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.ExtractAsync(zipPath, destPath, "zip"));

        Assert.Contains("Path traversal detected", ex.Message);
    }

    [Fact]
    public async Task RemoveInstalledFilesAsync_PathTraversal_IsRejected()
    {
        var installPath = Path.Combine(_tempDir, "installed");
        Directory.CreateDirectory(installPath);

        // Create a file outside the install path that we'll try to delete
        var outsideFile = Path.Combine(_tempDir, "precious.txt");
        File.WriteAllText(outsideFile, "important data");

        var package = new Packages.InstalledPackage(
            PackageId: "evil.package",
            Version: "1.0.0",
            Season: 2026,
            InstalledAt: DateTimeOffset.UtcNow,
            InstallPath: installPath,
            InstalledFiles: ["../precious.txt"]
        );

        var engine = new InstallEngine(new StubPlatformService());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RemoveInstalledFilesAsync(package));

        // Verify the file outside install dir was NOT deleted
        Assert.True(File.Exists(outsideFile));
    }

    [Fact]
    public async Task ExtractAsync_NormalZip_WorksCorrectly()
    {
        // Verify that normal extraction still works after security hardening
        var zipPath = Path.Combine(_tempDir, "normal.zip");
        var destPath = Path.Combine(_tempDir, "normal-extracted");
        Directory.CreateDirectory(destPath);

        using (var zipStream = File.Create(zipPath))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("subdir/test.txt");
            using (var writer = new StreamWriter(entry.Open()))
            {
                writer.Write("hello");
            }
        }

        var engine = new InstallEngine(new StubPlatformService());
        var files = await engine.ExtractAsync(zipPath, destPath, "zip");

        Assert.Single(files);
        Assert.Equal("subdir/test.txt", files[0]);
        Assert.True(File.Exists(Path.Combine(destPath, "subdir", "test.txt")));
    }
}
