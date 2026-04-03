using System.Diagnostics;
using System.Runtime.InteropServices;
using FrcToolsuite.Core.Platform;

namespace FrcToolsuite.Platform.macOS;

/// <summary>
/// macOS implementation of <see cref="IPlatformService"/>.
/// Uses symlinks and .command scripts for shortcuts, and modifies shell
/// profiles (~/.zshrc, ~/.bash_profile) for environment configuration.
/// </summary>
public class MacPlatformService : IPlatformService
{
    private static readonly string HomeDir =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public bool IsAdminElevated =>
        Environment.UserName == "root" || GetEffectiveUserId() == 0;

    public void CreateShortcut(string name, string targetPath, string? iconPath = null, bool isDesktop = false)
    {
        if (isDesktop)
        {
            // Create a .command wrapper script on the Desktop
            var desktopDir = Path.Combine(HomeDir, "Desktop");
            Directory.CreateDirectory(desktopDir);
            var scriptPath = Path.Combine(desktopDir, $"{SanitizeFileName(name)}.command");

            var script = $"""
                #!/bin/bash
                "{targetPath}" "$@"
                """;

            File.WriteAllText(scriptPath, script);
            SetFileExecutable(scriptPath);
        }
        else
        {
            // Create a symlink in ~/Applications
            var appsDir = Path.Combine(HomeDir, "Applications");
            Directory.CreateDirectory(appsDir);
            var linkPath = Path.Combine(appsDir, name);

            // Remove existing symlink if present
            if (File.Exists(linkPath) || Directory.Exists(linkPath))
            {
                try { File.Delete(linkPath); } catch { /* ignore */ }
            }

            File.CreateSymbolicLink(linkPath, targetPath);
        }
    }

    public void RemoveShortcut(string name, bool isDesktop = false)
    {
        if (isDesktop)
        {
            var desktopDir = Path.Combine(HomeDir, "Desktop");
            var scriptPath = Path.Combine(desktopDir, $"{SanitizeFileName(name)}.command");
            if (File.Exists(scriptPath))
            {
                File.Delete(scriptPath);
            }
        }
        else
        {
            var appsDir = Path.Combine(HomeDir, "Applications");
            var linkPath = Path.Combine(appsDir, name);
            if (File.Exists(linkPath))
            {
                File.Delete(linkPath);
            }
        }
    }

    public void AddToPath(string path)
    {
        ValidateShellSafe(path, nameof(path));
        var exportLine = $"export PATH=\"{path}:$PATH\"";
        var marker = $"# frc-toolsuite-path: {path}";
        var fullLine = $"{marker}\n{exportLine}";

        foreach (var profilePath in GetShellProfiles())
        {
            if (!File.Exists(profilePath))
            {
                continue;
            }

            var content = File.ReadAllText(profilePath);
            if (content.Contains(marker))
            {
                continue;
            }

            File.AppendAllText(profilePath, $"\n{fullLine}\n");
        }
    }

    public void RemoveFromPath(string path)
    {
        var marker = $"# frc-toolsuite-path: {path}";
        var exportLine = $"export PATH=\"{path}:$PATH\"";

        foreach (var profilePath in GetShellProfiles())
        {
            if (!File.Exists(profilePath))
            {
                continue;
            }

            var lines = File.ReadAllLines(profilePath).ToList();
            var filtered = lines
                .Where(l => l.TrimEnd() != marker && l.TrimEnd() != exportLine)
                .ToList();

            if (filtered.Count != lines.Count)
            {
                File.WriteAllLines(profilePath, filtered);
            }
        }
    }

    public void SetEnvironmentVariable(string name, string value)
    {
        ValidateShellSafe(name, nameof(name));
        ValidateShellSafe(value, nameof(value));
        var exportLine = $"export {name}=\"{value}\"";
        var marker = $"# frc-toolsuite-env: {name}";

        var profilePath = GetPrimaryProfile();
        EnsureFileExists(profilePath);

        var content = File.ReadAllText(profilePath);
        if (content.Contains(marker))
        {
            var lines = File.ReadAllLines(profilePath).ToList();
            var result = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].TrimEnd() == marker)
                {
                    result.Add(marker);
                    result.Add(exportLine);
                    if (i + 1 < lines.Count && lines[i + 1].TrimStart().StartsWith("export "))
                    {
                        i++;
                    }
                }
                else
                {
                    result.Add(lines[i]);
                }
            }

            File.WriteAllLines(profilePath, result);
        }
        else
        {
            File.AppendAllText(profilePath, $"\n{marker}\n{exportLine}\n");
        }
    }

    public void RemoveEnvironmentVariable(string name)
    {
        var marker = $"# frc-toolsuite-env: {name}";

        var profilePath = GetPrimaryProfile();
        if (!File.Exists(profilePath))
        {
            return;
        }

        var lines = File.ReadAllLines(profilePath).ToList();
        var result = new List<string>();
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimEnd() == marker)
            {
                if (i + 1 < lines.Count && lines[i + 1].TrimStart().StartsWith("export "))
                {
                    i++;
                }

                continue;
            }

            result.Add(lines[i]);
        }

        if (result.Count != lines.Count)
        {
            File.WriteAllLines(profilePath, result);
        }
    }

    public async Task RequestAdminElevationAsync(IReadOnlyList<string> operations)
    {
        var exePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Cannot determine current executable path.");

        var args = string.Join(" ", operations.Select(o => $"\"{o}\""));

        // Use osascript to display a native macOS admin prompt
        var escapedCommand = $"{exePath} {args}".Replace("\\", "\\\\").Replace("\"", "\\\"");
        var appleScript =
            $"do shell script \"\\\"{escapedCommand}\\\"\" with administrator privileges";

        var psi = new ProcessStartInfo
        {
            FileName = "osascript",
            Arguments = $"-e '{appleScript}'",
            UseShellExecute = false,
        };

        var process = Process.Start(psi);
        if (process is not null)
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
    }

    public string GetPlatformId()
    {
        return RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? "macos-arm64"
            : "macos-x64";
    }

    private static int GetEffectiveUserId()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "id",
                Arguments = "-u",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return -1;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return int.TryParse(output, out var uid) ? uid : -1;
        }
        catch
        {
            return -1;
        }
    }

    private static IReadOnlyList<string> GetShellProfiles()
    {
        var profiles = new List<string>
        {
            Path.Combine(HomeDir, ".zshrc"),
        };

        var bashProfile = Path.Combine(HomeDir, ".bash_profile");
        if (File.Exists(bashProfile))
        {
            profiles.Add(bashProfile);
        }

        return profiles;
    }

    private static string GetPrimaryProfile()
    {
        return Path.Combine(HomeDir, ".zshrc");
    }

    private static void SetFileExecutable(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            })?.WaitForExit(5000);
        }
        catch
        {
            // Best effort
        }
    }

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, "");
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
    }

    /// <summary>
    /// Validates that a value is safe to write into shell profile files.
    /// Rejects characters that could enable command injection.
    /// </summary>
    private static void ValidateShellSafe(string value, string paramName)
    {
        char[] dangerous = ['`', '$', '"', '\'', '\n', '\r', ';', '|', '&'];
        if (value.IndexOfAny(dangerous) >= 0)
        {
            throw new ArgumentException(
                $"Value contains unsafe shell characters and cannot be written to profile files.",
                paramName);
        }
    }
}
