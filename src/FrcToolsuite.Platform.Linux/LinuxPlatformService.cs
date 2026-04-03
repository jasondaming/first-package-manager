using System.Diagnostics;
using System.Runtime.InteropServices;
using FrcToolsuite.Core.Platform;

namespace FrcToolsuite.Platform.Linux;

/// <summary>
/// Linux implementation of <see cref="IPlatformService"/>.
/// Manages .desktop files, shell profile environment variables, and PATH entries.
/// </summary>
public class LinuxPlatformService : IPlatformService
{
    private static readonly string HomeDir =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public bool IsAdminElevated =>
        Environment.UserName == "root" || GetEffectiveUserId() == 0;

    public void CreateShortcut(string name, string targetPath, string? iconPath = null, bool isDesktop = false)
    {
        var folder = isDesktop
            ? Path.Combine(HomeDir, "Desktop")
            : Path.Combine(HomeDir, ".local", "share", "applications");

        Directory.CreateDirectory(folder);
        var desktopFilePath = Path.Combine(folder, $"{SanitizeFileName(name)}.desktop");

        var iconLine = !string.IsNullOrEmpty(iconPath) ? iconPath : "";

        var content = $"""
            [Desktop Entry]
            Version=1.0
            Type=Application
            Name={name}
            Exec={targetPath}
            Icon={iconLine}
            Terminal=false
            Categories=Development;Education;
            """;

        File.WriteAllText(desktopFilePath, content);
        SetFileExecutable(desktopFilePath);
    }

    public void RemoveShortcut(string name, bool isDesktop = false)
    {
        var folder = isDesktop
            ? Path.Combine(HomeDir, "Desktop")
            : Path.Combine(HomeDir, ".local", "share", "applications");

        var desktopFilePath = Path.Combine(folder, $"{SanitizeFileName(name)}.desktop");
        if (File.Exists(desktopFilePath))
        {
            File.Delete(desktopFilePath);
        }
    }

    public void AddToPath(string path)
    {
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
                continue; // Already present
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
        var exportLine = $"export {name}=\"{value}\"";
        var marker = $"# frc-toolsuite-env: {name}";

        var profilePath = GetPrimaryProfile();
        EnsureFileExists(profilePath);

        var content = File.ReadAllText(profilePath);
        if (content.Contains(marker))
        {
            // Replace existing entry
            var lines = File.ReadAllLines(profilePath).ToList();
            var result = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].TrimEnd() == marker)
                {
                    result.Add(marker);
                    result.Add(exportLine);
                    // Skip the next line (the old export)
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
                // Skip marker and following export line
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

        // Try pkexec first (graphical), fall back to sudo in a terminal
        var fileName = "pkexec";
        var arguments = $"\"{exePath}\" {args}";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
            };

            var process = Process.Start(psi);
            if (process is not null)
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            // pkexec not available, try sudo in a terminal emulator
            var termPsi = new ProcessStartInfo
            {
                FileName = "x-terminal-emulator",
                Arguments = $"-e sudo \"{exePath}\" {args}",
                UseShellExecute = false,
            };

            var termProcess = Process.Start(termPsi);
            if (termProcess is not null)
            {
                await termProcess.WaitForExitAsync().ConfigureAwait(false);
            }
        }
    }

    public string GetPlatformId()
    {
        return RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? "linux-arm64"
            : "linux-x64";
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
            Path.Combine(HomeDir, ".profile"),
            Path.Combine(HomeDir, ".bashrc"),
        };

        var zshrc = Path.Combine(HomeDir, ".zshrc");
        if (File.Exists(zshrc) || IsZshDefault())
        {
            profiles.Add(zshrc);
        }

        return profiles;
    }

    private static string GetPrimaryProfile()
    {
        return Path.Combine(HomeDir, ".profile");
    }

    private static bool IsZshDefault()
    {
        var shell = Environment.GetEnvironmentVariable("SHELL") ?? "";
        return shell.EndsWith("/zsh", StringComparison.Ordinal);
    }

    private static void SetFileExecutable(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"755 \"{path}\"",
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
}
