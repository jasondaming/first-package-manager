namespace FrcToolsuite.Core.Packages;

public class InstallPlan
{
    public List<InstallStep> Steps { get; set; } = [];

    public long TotalDownloadSize => Steps
        .Where(s => s.Action != InstallAction.Uninstall)
        .Sum(s => s.DownloadSize);

    public bool RequiresAdminElevation => Steps.Any(s => s.RequiresAdmin);
}

public class InstallStep
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public InstallAction Action { get; set; }
    public string? ArtifactUrl { get; set; }
    public long DownloadSize { get; set; }
    public bool RequiresAdmin { get; set; }

    /// <summary>
    /// The directory where Maven artifacts should be cached for offline Gradle builds.
    /// When set, the install step will extract a Maven bundle zip into this directory
    /// and run the metadata fixer.
    /// </summary>
    public string? MavenCacheDir { get; set; }

    /// <summary>
    /// URL to a pre-bundled zip of Maven artifacts for this package.
    /// </summary>
    public string? MavenBundleUrl { get; set; }

    /// <summary>
    /// URL of the vendordep JSON file to download and place in the WPILib vendordeps directory.
    /// </summary>
    public string? VendordepJsonUrl { get; set; }

    /// <summary>
    /// Local path where the vendordep JSON should be saved.
    /// </summary>
    public string? VendordepJsonPath { get; set; }
}

public enum InstallAction
{
    Install,
    Update,
    Uninstall
}

public record InstallProgress(
    int CurrentStep,
    int TotalSteps,
    string CurrentPackageId,
    long BytesDownloaded,
    long TotalBytes,
    InstallPhase Phase,
    IReadOnlyList<string>? SkippedPackages = null);

public enum InstallPhase
{
    Downloading,
    Extracting,
    Configuring,
    AwaitingAdmin
}
