using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using FrcToolsuite.Core.Platform;
using Microsoft.Win32;

namespace FrcToolsuite.Platform.Windows;

/// <summary>
/// Windows implementation of <see cref="IPlatformService"/>.
/// Uses Registry for environment variables, VBScript for shortcut creation,
/// and P/Invoke for broadcasting setting changes.
/// </summary>
public class WindowsPlatformService : IPlatformService
{
    private const int HWND_BROADCAST = 0xFFFF;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    public bool IsAdminElevated
    {
        get
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            return IsAdminElevatedWindows();
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsAdminElevatedWindows()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public void CreateShortcut(string name, string targetPath, string? iconPath = null, bool isDesktop = false)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var folder = isDesktop
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs");

        Directory.CreateDirectory(folder);
        var lnkPath = Path.Combine(folder, $"{name}.lnk");

        // Use VBScript via cscript to create the .lnk file
        var iconLine = !string.IsNullOrEmpty(iconPath)
            ? $"oLink.IconLocation = \"{EscapeVbs(iconPath)}\""
            : "";

        var vbs = $"""
            Set oWS = WScript.CreateObject("WScript.Shell")
            Set oLink = oWS.CreateShortcut("{EscapeVbs(lnkPath)}")
            oLink.TargetPath = "{EscapeVbs(targetPath)}"
            oLink.WorkingDirectory = "{EscapeVbs(Path.GetDirectoryName(targetPath) ?? "")}"
            {iconLine}
            oLink.Save
            """;

        ExecuteVbScript(vbs);
    }

    public void RemoveShortcut(string name, bool isDesktop = false)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var folder = isDesktop
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs");

        var lnkPath = Path.Combine(folder, $"{name}.lnk");
        if (File.Exists(lnkPath))
        {
            File.Delete(lnkPath);
        }
    }

    [SupportedOSPlatform("windows")]
    public void AddToPath(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        AddToPathWindows(path);
    }

    [SupportedOSPlatform("windows")]
    private void AddToPathWindows(string path)
    {
        using var envKey = Registry.CurrentUser.OpenSubKey("Environment", writable: true);
        if (envKey is null)
        {
            return;
        }

        var currentPath = envKey.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
        var paths = currentPath
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (paths.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
        {
            return; // Already present
        }

        paths.Add(path);
        envKey.SetValue("Path", string.Join(';', paths), RegistryValueKind.ExpandString);
        BroadcastSettingChange();
    }

    [SupportedOSPlatform("windows")]
    public void RemoveFromPath(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        RemoveFromPathWindows(path);
    }

    [SupportedOSPlatform("windows")]
    private void RemoveFromPathWindows(string path)
    {
        using var envKey = Registry.CurrentUser.OpenSubKey("Environment", writable: true);
        if (envKey is null)
        {
            return;
        }

        var currentPath = envKey.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
        var paths = currentPath
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase))
            .ToList();

        envKey.SetValue("Path", string.Join(';', paths), RegistryValueKind.ExpandString);
        BroadcastSettingChange();
    }

    [SupportedOSPlatform("windows")]
    public void SetEnvironmentVariable(string name, string value)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        SetEnvironmentVariableWindows(name, value);
    }

    [SupportedOSPlatform("windows")]
    private void SetEnvironmentVariableWindows(string name, string value)
    {
        using var envKey = Registry.CurrentUser.OpenSubKey("Environment", writable: true);
        envKey?.SetValue(name, value, RegistryValueKind.String);
        BroadcastSettingChange();
    }

    [SupportedOSPlatform("windows")]
    public void RemoveEnvironmentVariable(string name)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        RemoveEnvironmentVariableWindows(name);
    }

    [SupportedOSPlatform("windows")]
    private void RemoveEnvironmentVariableWindows(string name)
    {
        using var envKey = Registry.CurrentUser.OpenSubKey("Environment", writable: true);
        envKey?.DeleteValue(name, throwOnMissingValue: false);
        BroadcastSettingChange();
    }

    public async Task RequestAdminElevationAsync(IReadOnlyList<string> operations)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var exePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Cannot determine current executable path.");

        var args = string.Join(" ", operations.Select(o => $"\"{o}\""));

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            UseShellExecute = true,
            Verb = "runas",
        };

        var process = Process.Start(psi);
        if (process is not null)
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
    }

    public string GetPlatformId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "windows-arm64"
                : "windows-x64";
        }

        return "unknown";
    }

    private static void BroadcastSettingChange()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        SendMessageTimeout(
            (IntPtr)HWND_BROADCAST,
            WM_SETTINGCHANGE,
            UIntPtr.Zero,
            "Environment",
            SMTO_ABORTIFHUNG,
            5000,
            out _);
    }

    private static string EscapeVbs(string value)
    {
        return value.Replace("\"", "\"\"").Replace("\\", "\\\\");
    }

    private static void ExecuteVbScript(string script)
    {
        var tempVbs = Path.Combine(Path.GetTempPath(), $"frc_shortcut_{Guid.NewGuid():N}.vbs");
        try
        {
            File.WriteAllText(tempVbs, script);
            var psi = new ProcessStartInfo
            {
                FileName = "cscript",
                Arguments = $"//Nologo \"{tempVbs}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(10_000);
        }
        finally
        {
            try
            {
                if (File.Exists(tempVbs))
                {
                    File.Delete(tempVbs);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
