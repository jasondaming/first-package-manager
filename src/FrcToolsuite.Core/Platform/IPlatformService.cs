namespace FrcToolsuite.Core.Platform;

public interface IPlatformService
{
    void CreateShortcut(string name, string targetPath, string? iconPath = null, bool isDesktop = false);
    void RemoveShortcut(string name, bool isDesktop = false);
    void AddToPath(string path);
    void RemoveFromPath(string path);
    void SetEnvironmentVariable(string name, string value);
    void RemoveEnvironmentVariable(string name);
    bool IsAdminElevated { get; }
    Task RequestAdminElevationAsync(IReadOnlyList<string> operations);
    string GetPlatformId();
}
