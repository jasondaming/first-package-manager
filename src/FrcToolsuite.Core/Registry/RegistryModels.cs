using System.Text.Json.Serialization;

namespace FrcToolsuite.Core.Registry;

public class RegistryIndex
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; set; }

    [JsonPropertyName("seasons")]
    public List<SeasonInfo> Seasons { get; set; } = [];

    [JsonPropertyName("publishers")]
    public List<PublisherInfo> Publishers { get; set; } = [];

    [JsonPropertyName("packages")]
    public List<PackageSummary> Packages { get; set; } = [];

    [JsonPropertyName("bundles")]
    public List<BundleRef> Bundles { get; set; } = [];
}

public class BundleRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class SeasonInfo
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("kickoffDate")]
    public DateTimeOffset? KickoffDate { get; set; }
}

public class PublisherInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("trusted")]
    public bool Trusted { get; set; }
}

public class PackageSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("competition")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CompetitionProgram Competition { get; set; }

    [JsonPropertyName("publisherId")]
    public string PublisherId { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("totalSize")]
    public Dictionary<string, long> TotalSize { get; set; } = new();

    [JsonPropertyName("manifestUrl")]
    public string ManifestUrl { get; set; } = string.Empty;

    [JsonPropertyName("manifestSha256")]
    public string ManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("requiresAdmin")]
    public bool RequiresAdmin { get; set; }
}

public class PackageManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("publisher")]
    public string Publisher { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("competition")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CompetitionProgram Competition { get; set; }

    [JsonPropertyName("license")]
    public string? License { get; set; }

    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    [JsonPropertyName("repository")]
    public string? Repository { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("dependencies")]
    public List<PackageDependency> Dependencies { get; set; } = [];

    [JsonPropertyName("conflicts")]
    public List<string> Conflicts { get; set; } = [];

    [JsonPropertyName("artifacts")]
    public Dictionary<string, PackageArtifact> Artifacts { get; set; } = new();

    [JsonPropertyName("install")]
    public InstallInfo? Install { get; set; }

    [JsonPropertyName("mavenArtifacts")]
    public MavenArtifacts? MavenArtifacts { get; set; }

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; set; }

    [JsonPropertyName("supersededBy")]
    public string? SupersededBy { get; set; }
}

public class PackageArtifact
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("archiveType")]
    public string? ArchiveType { get; set; }

    [JsonPropertyName("extractPath")]
    public string? ExtractPath { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("mirrors")]
    public List<string> Mirrors { get; set; } = [];
}

public class PackageDependency
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("versionRange")]
    public string? VersionRange { get; set; }

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DependencyType Type { get; set; } = DependencyType.Required;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public class InstallInfo
{
    [JsonPropertyName("installDir")]
    public string? InstallDir { get; set; }

    [JsonPropertyName("requiresAdmin")]
    public bool RequiresAdmin { get; set; }

    [JsonPropertyName("postInstall")]
    public List<PostInstallAction> PostInstall { get; set; } = [];
}

public class PostInstallAction
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("params")]
    public Dictionary<string, string> Params { get; set; } = new();
}

public class MavenArtifacts
{
    [JsonPropertyName("vendordepJson")]
    public string? VendordepJson { get; set; }

    [JsonPropertyName("repositories")]
    public List<string> Repositories { get; set; } = [];

    [JsonPropertyName("coordinates")]
    public List<MavenCoordinate> Coordinates { get; set; } = [];

    [JsonPropertyName("runMetadataFixer")]
    public bool RunMetadataFixer { get; set; }
}

public class MavenCoordinate
{
    [JsonPropertyName("groupId")]
    public string GroupId { get; set; } = string.Empty;

    [JsonPropertyName("artifactId")]
    public string ArtifactId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("classifiers")]
    public List<string> Classifiers { get; set; } = [];

    [JsonPropertyName("sha256")]
    public Dictionary<string, string> Sha256 { get; set; } = new();
}

public class BundleDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("competition")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CompetitionProgram Competition { get; set; }

    [JsonPropertyName("audience")]
    public string? Audience { get; set; }

    [JsonPropertyName("packages")]
    public List<BundlePackageRef> Packages { get; set; } = [];
}

public class BundlePackageRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("inclusion")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PackageInclusion Inclusion { get; set; } = PackageInclusion.Required;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public class TeamProfile
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("profileName")]
    public string ProfileName { get; set; } = string.Empty;

    [JsonPropertyName("teamNumber")]
    public int TeamNumber { get; set; }

    [JsonPropertyName("competition")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CompetitionProgram Competition { get; set; }

    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("baseBundle")]
    public string? BaseBundle { get; set; }

    [JsonPropertyName("packages")]
    public List<string> Packages { get; set; } = [];

    [JsonPropertyName("installOptions")]
    public Dictionary<string, string>? InstallOptions { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompetitionProgram
{
    Frc,
    Ftc
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DependencyType
{
    Required,
    Recommended,
    Optional
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PackageInclusion
{
    Required,
    Default,
    Optional
}
