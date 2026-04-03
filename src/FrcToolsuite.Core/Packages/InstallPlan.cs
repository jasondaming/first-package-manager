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
    InstallPhase Phase);

public enum InstallPhase
{
    Downloading,
    Extracting,
    Configuring
}
