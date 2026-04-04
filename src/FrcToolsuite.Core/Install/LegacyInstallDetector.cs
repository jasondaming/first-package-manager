namespace FrcToolsuite.Core.Install;

/// <summary>
/// Detects existing WPILib installations from the legacy installer
/// (WPILibInstaller-Avalonia) which installs to C:\Users\Public\wpilib\{year}\.
/// This allows the package manager to show already-installed tools
/// without requiring them to be reinstalled through the new system.
/// </summary>
public static class LegacyInstallDetector
{
    /// <summary>
    /// Returns a set of package IDs that appear to be installed by the legacy WPILib installer.
    /// </summary>
    public static HashSet<string> DetectInstalledPackages(int season = 2026)
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var wpilibDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
                .Replace("ProgramData", "Users\\Public"),
            "wpilib",
            season.ToString());

        // Try both common locations
        var candidates = new[]
        {
            wpilibDir,
            Path.Combine("C:\\Users\\Public\\wpilib", season.ToString()),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "wpilib",
                season.ToString())
        };

        string? baseDir = null;
        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                baseDir = candidate;
                break;
            }
        }

        if (baseDir == null)
        {
            return installed;
        }

        // Map legacy directories to our package IDs
        if (Directory.Exists(Path.Combine(baseDir, "jdk")))
        {
            installed.Add("wpilib.jdk");
        }

        if (Directory.Exists(Path.Combine(baseDir, "vscode")))
        {
            installed.Add("wpilib.vscode");
        }

        if (Directory.Exists(Path.Combine(baseDir, "tools")))
        {
            installed.Add("wpilib.tools");
        }

        if (Directory.Exists(Path.Combine(baseDir, "maven")))
        {
            installed.Add("wpilib.gradlerio");
        }

        if (Directory.Exists(Path.Combine(baseDir, "advantagescope")))
        {
            installed.Add("wpilib.advantagescope");
        }

        if (Directory.Exists(Path.Combine(baseDir, "elastic")))
        {
            installed.Add("wpilib.elastic");
        }

        // Check for vendor libraries in the vendordeps directory
        var vendordepsDir = Path.Combine(baseDir, "vendordeps");
        if (Directory.Exists(vendordepsDir))
        {
            foreach (var file in Directory.GetFiles(vendordepsDir, "*.json"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();

                if (fileName.Contains("phoenix"))
                {
                    installed.Add("ctre.phoenix6");
                }
                else if (fileName.Contains("revlib"))
                {
                    installed.Add("rev.revlib");
                }
                else if (fileName.Contains("pathplanner"))
                {
                    installed.Add("pathplanner.pathplannerlib");
                }
                else if (fileName.Contains("photon"))
                {
                    installed.Add("photonvision.photonlib");
                }
                else if (fileName.Contains("yagsl"))
                {
                    installed.Add("community.yagsl");
                }
                else if (fileName.Contains("advantagekit"))
                {
                    installed.Add("community.advantagekit");
                }
            }
        }

        return installed;
    }
}
