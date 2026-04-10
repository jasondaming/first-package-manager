using System.Xml.Linq;
using FrcToolsuite.Core.Install;

namespace FrcToolsuite.Core.Tests;

public class MavenMetadataFixerTests : IDisposable
{
    private readonly string _tempDir;

    public MavenMetadataFixerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "maven-fixer-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task FixMetadataAsync_SinglePom_GeneratesMetadata()
    {
        // Arrange: com/ctre/phoenix6/wpiapi-java/26.1.0/wpiapi-java-26.1.0.pom
        var pomDir = Path.Combine(_tempDir, "com", "ctre", "phoenix6", "wpiapi-java", "26.1.0");
        Directory.CreateDirectory(pomDir);
        var pomContent = CreateMinimalPom("com.ctre.phoenix6", "wpiapi-java", "26.1.0");
        await File.WriteAllTextAsync(Path.Combine(pomDir, "wpiapi-java-26.1.0.pom"), pomContent);

        // Act
        await MavenMetadataFixer.FixMetadataAsync(_tempDir);

        // Assert
        var metadataPath = Path.Combine(_tempDir, "com", "ctre", "phoenix6", "wpiapi-java", "maven-metadata.xml");
        Assert.True(File.Exists(metadataPath), "maven-metadata.xml should be created");

        var doc = XDocument.Load(metadataPath);
        var root = doc.Root!;

        Assert.Equal("com.ctre.phoenix6", root.Element("groupId")!.Value);
        Assert.Equal("wpiapi-java", root.Element("artifactId")!.Value);

        var versioning = root.Element("versioning")!;
        Assert.Equal("26.1.0", versioning.Element("latest")!.Value);
        Assert.Equal("26.1.0", versioning.Element("release")!.Value);

        var versions = versioning.Element("versions")!.Elements("version").Select(e => e.Value).ToList();
        Assert.Single(versions);
        Assert.Equal("26.1.0", versions[0]);
    }

    [Fact]
    public async Task FixMetadataAsync_MultipleVersions_ListsAllVersionsSorted()
    {
        // Arrange: two versions of the same artifact
        foreach (var version in new[] { "26.1.0", "26.0.0", "26.2.0" })
        {
            var pomDir = Path.Combine(_tempDir, "com", "revrobotics", "rev-lib", version);
            Directory.CreateDirectory(pomDir);
            var pomContent = CreateMinimalPom("com.revrobotics", "rev-lib", version);
            await File.WriteAllTextAsync(Path.Combine(pomDir, $"rev-lib-{version}.pom"), pomContent);
        }

        // Act
        await MavenMetadataFixer.FixMetadataAsync(_tempDir);

        // Assert
        var metadataPath = Path.Combine(_tempDir, "com", "revrobotics", "rev-lib", "maven-metadata.xml");
        Assert.True(File.Exists(metadataPath));

        var doc = XDocument.Load(metadataPath);
        var versioning = doc.Root!.Element("versioning")!;

        var versions = versioning.Element("versions")!.Elements("version").Select(e => e.Value).ToList();
        Assert.Equal(3, versions.Count);
        Assert.Equal("26.0.0", versions[0]);
        Assert.Equal("26.1.0", versions[1]);
        Assert.Equal("26.2.0", versions[2]);

        // Latest should be the last sorted version
        Assert.Equal("26.2.0", versioning.Element("latest")!.Value);
        Assert.Equal("26.2.0", versioning.Element("release")!.Value);
    }

    [Fact]
    public async Task FixMetadataAsync_MultipleArtifacts_GeneratesSeparateMetadata()
    {
        // Arrange: two different artifacts
        var pomDir1 = Path.Combine(_tempDir, "com", "example", "lib-a", "1.0.0");
        Directory.CreateDirectory(pomDir1);
        await File.WriteAllTextAsync(
            Path.Combine(pomDir1, "lib-a-1.0.0.pom"),
            CreateMinimalPom("com.example", "lib-a", "1.0.0"));

        var pomDir2 = Path.Combine(_tempDir, "com", "example", "lib-b", "2.0.0");
        Directory.CreateDirectory(pomDir2);
        await File.WriteAllTextAsync(
            Path.Combine(pomDir2, "lib-b-2.0.0.pom"),
            CreateMinimalPom("com.example", "lib-b", "2.0.0"));

        // Act
        await MavenMetadataFixer.FixMetadataAsync(_tempDir);

        // Assert
        Assert.True(File.Exists(Path.Combine(_tempDir, "com", "example", "lib-a", "maven-metadata.xml")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "com", "example", "lib-b", "maven-metadata.xml")));

        var docA = XDocument.Load(Path.Combine(_tempDir, "com", "example", "lib-a", "maven-metadata.xml"));
        Assert.Equal("lib-a", docA.Root!.Element("artifactId")!.Value);

        var docB = XDocument.Load(Path.Combine(_tempDir, "com", "example", "lib-b", "maven-metadata.xml"));
        Assert.Equal("lib-b", docB.Root!.Element("artifactId")!.Value);
    }

    [Fact]
    public async Task FixMetadataAsync_EmptyDirectory_DoesNotThrow()
    {
        // Act & Assert: should not throw
        await MavenMetadataFixer.FixMetadataAsync(_tempDir);
    }

    [Fact]
    public async Task FixMetadataAsync_NonExistentDirectory_DoesNotThrow()
    {
        var nonExistent = Path.Combine(_tempDir, "does-not-exist");

        // Act & Assert: should not throw
        await MavenMetadataFixer.FixMetadataAsync(nonExistent);
    }

    [Fact]
    public async Task FixMetadataAsync_HasLastUpdatedTimestamp()
    {
        var pomDir = Path.Combine(_tempDir, "org", "test", "artifact", "1.0.0");
        Directory.CreateDirectory(pomDir);
        await File.WriteAllTextAsync(
            Path.Combine(pomDir, "artifact-1.0.0.pom"),
            CreateMinimalPom("org.test", "artifact", "1.0.0"));

        await MavenMetadataFixer.FixMetadataAsync(_tempDir);

        var doc = XDocument.Load(
            Path.Combine(_tempDir, "org", "test", "artifact", "maven-metadata.xml"));
        var lastUpdated = doc.Root!.Element("versioning")!.Element("lastUpdated")!.Value;

        // Should be a 14-digit timestamp like 20260403120000
        Assert.Equal(14, lastUpdated.Length);
        Assert.True(long.TryParse(lastUpdated, out _), "lastUpdated should be numeric");
    }

    [Fact]
    public void ParseArtifactInfo_FromDirectoryStructure_ExtractsCorrectly()
    {
        // Simulate: mavenRoot/com/ctre/phoenix6/wpiapi-java/26.1.0/wpiapi-java-26.1.0.pom
        var pomPath = Path.Combine(
            _tempDir, "com", "ctre", "phoenix6", "wpiapi-java", "26.1.0", "wpiapi-java-26.1.0.pom");

        var (groupId, artifactId, version) = MavenMetadataFixer.ParseArtifactInfo(pomPath, _tempDir);

        Assert.Equal("com.ctre.phoenix6", groupId);
        Assert.Equal("wpiapi-java", artifactId);
        Assert.Equal("26.1.0", version);
    }

    [Fact]
    public void BuildMetadataXml_ProducesValidXml()
    {
        var xml = MavenMetadataFixer.BuildMetadataXml(
            "com.example", "my-lib",
            new List<string> { "1.0.0", "2.0.0" },
            "2.0.0", "20260403120000");

        var doc = XDocument.Parse(xml);
        var root = doc.Root!;

        Assert.Equal("metadata", root.Name.LocalName);
        Assert.Equal("com.example", root.Element("groupId")!.Value);
        Assert.Equal("my-lib", root.Element("artifactId")!.Value);
        Assert.Equal("2.0.0", root.Element("versioning")!.Element("latest")!.Value);
    }

    private static string CreateMinimalPom(string groupId, string artifactId, string version)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <project xmlns="http://maven.apache.org/POM/4.0.0">
              <modelVersion>4.0.0</modelVersion>
              <groupId>{groupId}</groupId>
              <artifactId>{artifactId}</artifactId>
              <version>{version}</version>
            </project>
            """;
    }
}
