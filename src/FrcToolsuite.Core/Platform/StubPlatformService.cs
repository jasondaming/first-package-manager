using System.Runtime.InteropServices;

namespace FrcToolsuite.Core.Platform;

/// <summary>
/// A no-op implementation of <see cref="IPlatformService"/> for development and testing.
/// All methods log what they would do but do not modify the system.
/// <see cref="GetPlatformId"/> returns the real platform identifier.
/// </summary>
public class StubPlatformService : IPlatformService
{
    public bool IsAdminElevated => false;

    public void CreateShortcut(string name, string targetPath, string? iconPath = null, bool isDesktop = false)
    {
        // Stub: would create shortcut "{name}" -> "{targetPath}" (desktop={isDesktop})
    }

    public void RemoveShortcut(string name, bool isDesktop = false)
    {
        // Stub: would remove shortcut "{name}" (desktop={isDesktop})
    }

    public void AddToPath(string path)
    {
        // Stub: would add "{path}" to PATH
    }

    public void RemoveFromPath(string path)
    {
        // Stub: would remove "{path}" from PATH
    }

    public void SetEnvironmentVariable(string name, string value)
    {
        // Stub: would set env var "{name}" = "{value}"
    }

    public void RemoveEnvironmentVariable(string name)
    {
        // Stub: would remove env var "{name}"
    }

    public Task RequestAdminElevationAsync(IReadOnlyList<string> operations)
    {
        // Stub: would request admin elevation for operations
        return Task.CompletedTask;
    }

    public string GetPlatformId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "windows-arm64"
                : "windows-x64";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "macos-arm64"
                : "macos-x64";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "linux-arm64"
                : "linux-x64";
        }

        return "unknown";
    }
}
