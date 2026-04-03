using System.Text.Json.Serialization;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Configuration;

public class AppSettings
{
    [JsonPropertyName("installDirectory")]
    public string InstallDirectory { get; set; } = string.Empty;

    [JsonPropertyName("cacheDirectory")]
    public string CacheDirectory { get; set; } = string.Empty;

    [JsonPropertyName("proxyUrl")]
    public string? ProxyUrl { get; set; }

    [JsonPropertyName("autoCheckUpdates")]
    public bool AutoCheckUpdates { get; set; } = true;

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "system";

    [JsonPropertyName("lastRegistryFetch")]
    public DateTimeOffset? LastRegistryFetch { get; set; }

    [JsonPropertyName("selectedProgram")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CompetitionProgram SelectedProgram { get; set; } = CompetitionProgram.Frc;

    [JsonPropertyName("selectedSeason")]
    public int SelectedSeason { get; set; }
}
