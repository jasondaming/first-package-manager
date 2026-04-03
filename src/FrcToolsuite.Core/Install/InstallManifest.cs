using System.Text.Json.Serialization;

namespace FrcToolsuite.Core.Install;

public class InstallManifest
{
    [JsonPropertyName("packageId")]
    public string PackageId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("season")]
    public string Season { get; set; } = string.Empty;

    [JsonPropertyName("installedAt")]
    public DateTime InstalledAt { get; set; }

    [JsonPropertyName("installPath")]
    public string InstallPath { get; set; } = string.Empty;

    [JsonPropertyName("installedFiles")]
    public string[] InstalledFiles { get; set; } = [];

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty;
}
