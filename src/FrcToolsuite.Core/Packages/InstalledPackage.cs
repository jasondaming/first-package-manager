namespace FrcToolsuite.Core.Packages;

public record InstalledPackage(
    string PackageId,
    string Version,
    int Season,
    DateTimeOffset InstalledAt,
    string InstallPath,
    string[] InstalledFiles);
