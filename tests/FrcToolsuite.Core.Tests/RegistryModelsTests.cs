using System.Text.Json;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Tests;

public class RegistryModelsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void PackageManifest_RoundTrip_PreservesAllFields()
    {
        var manifest = new PackageManifest
        {
            Id = "wpilib",
            Name = "WPILib",
            Version = "2026.1.1",
            Season = 2026,
            Publisher = "WPI",
            Description = "Core FRC framework",
            Category = "Runtime",
            Tags = ["frc", "core"],
            Competition = CompetitionProgram.Frc,
            License = "BSD-3-Clause",
            Homepage = "https://wpilib.org",
            Repository = "https://github.com/wpilibsuite/allwpilib",
            Dependencies =
            [
                new PackageDependency
                {
                    Id = "java-jdk",
                    VersionRange = ">=17.0.0",
                    Type = DependencyType.Required,
                    Reason = "Java runtime required"
                }
            ],
            Artifacts = new Dictionary<string, PackageArtifact>
            {
                ["windows-x64"] = new PackageArtifact
                {
                    Url = "https://example.com/wpilib-win64.zip",
                    Sha256 = "abc123",
                    Size = 1_200_000_000,
                    ArchiveType = "zip",
                    Mirrors = ["https://mirror.example.com/wpilib-win64.zip"]
                }
            },
            Install = new InstallInfo
            {
                InstallDir = "C:\\frc",
                RequiresAdmin = false,
                PostInstall =
                [
                    new PostInstallAction
                    {
                        Action = "set-env",
                        Platform = "windows",
                        Params = new Dictionary<string, string> { ["JAVA_HOME"] = "jdk" }
                    }
                ]
            }
        };

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<PackageManifest>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(manifest.Id, deserialized.Id);
        Assert.Equal(manifest.Name, deserialized.Name);
        Assert.Equal(manifest.Version, deserialized.Version);
        Assert.Equal(manifest.Season, deserialized.Season);
        Assert.Equal(manifest.Publisher, deserialized.Publisher);
        Assert.Equal(manifest.Competition, deserialized.Competition);
        Assert.Equal(manifest.License, deserialized.License);
        Assert.Equal(manifest.Homepage, deserialized.Homepage);
        Assert.Equal(manifest.Repository, deserialized.Repository);
        Assert.Equal(manifest.Tags.Count, deserialized.Tags.Count);
        Assert.Equal(manifest.Dependencies.Count, deserialized.Dependencies.Count);
        Assert.Equal(manifest.Dependencies[0].Id, deserialized.Dependencies[0].Id);
        Assert.Equal(manifest.Dependencies[0].Type, deserialized.Dependencies[0].Type);
        Assert.Equal(manifest.Artifacts.Count, deserialized.Artifacts.Count);
        Assert.True(deserialized.Artifacts.ContainsKey("windows-x64"));
        Assert.Equal(manifest.Install!.InstallDir, deserialized.Install!.InstallDir);
        Assert.Equal(manifest.Install.PostInstall.Count, deserialized.Install.PostInstall.Count);
    }

    [Fact]
    public void RegistryIndex_Deserializes_FromJson()
    {
        var json = """
            {
                "schemaVersion": "1.0",
                "lastUpdated": "2026-03-15T12:00:00Z",
                "seasons": [
                    { "year": 2026, "status": "active", "kickoffDate": "2026-01-04T00:00:00Z" }
                ],
                "publishers": [
                    { "id": "wpi", "name": "WPI", "trusted": true }
                ],
                "packages": [
                    {
                        "id": "wpilib",
                        "name": "WPILib",
                        "season": 2026,
                        "version": "2026.1.1",
                        "category": "Runtime",
                        "competition": "Frc",
                        "publisherId": "wpi",
                        "tags": ["frc"],
                        "description": "Core framework",
                        "totalSize": { "windows-x64": 1200000000 },
                        "manifestUrl": "https://example.com/wpilib.json",
                        "manifestSha256": "abc123"
                    }
                ]
            }
            """;

        var index = JsonSerializer.Deserialize<RegistryIndex>(json, JsonOptions);

        Assert.NotNull(index);
        Assert.Equal("1.0", index.SchemaVersion);
        Assert.Single(index.Seasons);
        Assert.Equal(2026, index.Seasons[0].Year);
        Assert.Equal("active", index.Seasons[0].Status);
        Assert.Single(index.Publishers);
        Assert.True(index.Publishers[0].Trusted);
        Assert.Single(index.Packages);
        Assert.Equal("wpilib", index.Packages[0].Id);
        Assert.Equal(CompetitionProgram.Frc, index.Packages[0].Competition);
    }

    [Fact]
    public void BundleDefinition_Deserializes_FromJson()
    {
        var json = """
            {
                "id": "frc-starter-2026",
                "name": "FRC Starter Kit",
                "season": 2026,
                "description": "Everything to start FRC development",
                "competition": "Frc",
                "audience": "beginner",
                "packages": [
                    { "id": "wpilib", "inclusion": "Required", "reason": "Core framework" },
                    { "id": "advantagescope", "inclusion": "Default", "reason": "Telemetry tool" },
                    { "id": "pathplanner", "inclusion": "Optional", "reason": "Auto path planning" }
                ]
            }
            """;

        var bundle = JsonSerializer.Deserialize<BundleDefinition>(json, JsonOptions);

        Assert.NotNull(bundle);
        Assert.Equal("frc-starter-2026", bundle.Id);
        Assert.Equal("FRC Starter Kit", bundle.Name);
        Assert.Equal(2026, bundle.Season);
        Assert.Equal(CompetitionProgram.Frc, bundle.Competition);
        Assert.Equal("beginner", bundle.Audience);
        Assert.Equal(3, bundle.Packages.Count);
        Assert.Equal(PackageInclusion.Required, bundle.Packages[0].Inclusion);
        Assert.Equal(PackageInclusion.Default, bundle.Packages[1].Inclusion);
        Assert.Equal(PackageInclusion.Optional, bundle.Packages[2].Inclusion);
    }

    [Fact]
    public void PackageManifest_DefaultValues_AreCorrect()
    {
        var manifest = new PackageManifest();

        Assert.Equal(string.Empty, manifest.Id);
        Assert.Equal(string.Empty, manifest.Name);
        Assert.Empty(manifest.Tags);
        Assert.Empty(manifest.Dependencies);
        Assert.Empty(manifest.Conflicts);
        Assert.Empty(manifest.Artifacts);
        Assert.Null(manifest.Install);
        Assert.Null(manifest.MavenArtifacts);
        Assert.False(manifest.Deprecated);
        Assert.Null(manifest.SupersededBy);
    }

    [Fact]
    public void TeamProfile_RoundTrip_PreservesFields()
    {
        var profile = new TeamProfile
        {
            SchemaVersion = "1.0",
            ProfileName = "Competition Setup",
            TeamNumber = 254,
            Competition = CompetitionProgram.Frc,
            Season = 2026,
            CreatedAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            BaseBundle = "frc-starter-2026",
            Packages = ["wpilib", "revlib", "advantagescope"],
            Notes = "Standard competition setup"
        };

        var json = JsonSerializer.Serialize(profile, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<TeamProfile>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(profile.TeamNumber, deserialized.TeamNumber);
        Assert.Equal(profile.Competition, deserialized.Competition);
        Assert.Equal(profile.BaseBundle, deserialized.BaseBundle);
        Assert.Equal(profile.Packages.Count, deserialized.Packages.Count);
    }
}
