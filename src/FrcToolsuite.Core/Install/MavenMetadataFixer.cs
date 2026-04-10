using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace FrcToolsuite.Core.Install;

/// <summary>
/// Scans a Maven repository directory for POM files and generates
/// maven-metadata.xml files for each artifact, listing all available versions.
/// This is required for offline Gradle builds to resolve dependencies.
/// </summary>
/// <remarks>
/// C# port of WPILib's MavenMetaDataFixer Java tool. The Maven directory
/// structure encodes the metadata: a POM at
/// <c>com/ctre/phoenix6/wpiapi-java/26.1.0/wpiapi-java-26.1.0.pom</c>
/// indicates groupId=com.ctre.phoenix6, artifactId=wpiapi-java, version=26.1.0.
/// This class can also parse the POM XML to extract groupId/version when they
/// differ from the directory structure (e.g., inherited from a parent POM).
/// </remarks>
public static class MavenMetadataFixer
{
    /// <summary>
    /// Scans <paramref name="mavenDir"/> for POM files, groups them by
    /// groupId + artifactId, and writes a <c>maven-metadata.xml</c> for
    /// each artifact listing all discovered versions.
    /// </summary>
    public static async Task FixMetadataAsync(string mavenDir, CancellationToken ct = default)
    {
        if (!Directory.Exists(mavenDir))
        {
            return;
        }

        var artifactVersions = new Dictionary<(string GroupId, string ArtifactId), List<string>>();

        // Recursively find all .pom files
        var pomFiles = Directory.EnumerateFiles(mavenDir, "*.pom", SearchOption.AllDirectories);

        foreach (var pomFile in pomFiles)
        {
            ct.ThrowIfCancellationRequested();

            var (groupId, artifactId, version) = ParseArtifactInfo(pomFile, mavenDir);
            if (groupId == null || artifactId == null || version == null)
            {
                continue;
            }

            var key = (groupId, artifactId);
            if (!artifactVersions.TryGetValue(key, out var versions))
            {
                versions = [];
                artifactVersions[key] = versions;
            }

            if (!versions.Contains(version))
            {
                versions.Add(version);
            }
        }

        // Write maven-metadata.xml for each artifact
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        foreach (var ((groupId, artifactId), versions) in artifactVersions)
        {
            ct.ThrowIfCancellationRequested();

            versions.Sort(StringComparer.OrdinalIgnoreCase);
            var latest = versions[^1];

            var metadataXml = BuildMetadataXml(groupId, artifactId, versions, latest, timestamp);

            var groupPath = groupId.Replace('.', Path.DirectorySeparatorChar);
            var metadataDir = Path.Combine(mavenDir, groupPath, artifactId);
            Directory.CreateDirectory(metadataDir);

            var metadataPath = Path.Combine(metadataDir, "maven-metadata.xml");
            await File.WriteAllBytesAsync(metadataPath, System.Text.Encoding.UTF8.GetBytes(metadataXml), ct);
        }
    }

    /// <summary>
    /// Extracts groupId, artifactId, and version from a POM file.
    /// First attempts to parse from the directory structure, then falls back
    /// to reading the POM XML if needed.
    /// </summary>
    internal static (string? GroupId, string? ArtifactId, string? Version) ParseArtifactInfo(
        string pomFilePath, string mavenRoot)
    {
        // Try to derive from directory structure first:
        // mavenRoot/group/parts/artifactId/version/artifactId-version.pom
        var relativePath = Path.GetRelativePath(mavenRoot, pomFilePath);
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Need at least: groupPart(s) / artifactId / version / filename.pom
        // Minimum: group / artifactId / version / file.pom = 4 parts
        if (parts.Length >= 4)
        {
            var version = parts[^2];
            var artifactId = parts[^3];
            var groupParts = parts[..^3];
            var groupId = string.Join('.', groupParts);

            if (!string.IsNullOrEmpty(groupId) &&
                !string.IsNullOrEmpty(artifactId) &&
                !string.IsNullOrEmpty(version))
            {
                return (groupId, artifactId, version);
            }
        }

        // Fall back to parsing the POM XML
        return ParsePomXml(pomFilePath);
    }

    private static (string? GroupId, string? ArtifactId, string? Version) ParsePomXml(string pomFilePath)
    {
        try
        {
            var doc = XDocument.Load(pomFilePath);
            var root = doc.Root;
            if (root == null)
            {
                return (null, null, null);
            }

            var ns = root.GetDefaultNamespace();

            var groupId = root.Element(ns + "groupId")?.Value;
            var artifactId = root.Element(ns + "artifactId")?.Value;
            var version = root.Element(ns + "version")?.Value;

            // Fall back to parent element if missing
            var parent = root.Element(ns + "parent");
            if (parent != null)
            {
                groupId ??= parent.Element(ns + "groupId")?.Value;
                version ??= parent.Element(ns + "version")?.Value;
            }

            return (groupId, artifactId, version);
        }
        catch (XmlException)
        {
            return (null, null, null);
        }
    }

    internal static string BuildMetadataXml(
        string groupId,
        string artifactId,
        List<string> versions,
        string latest,
        string timestamp)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("metadata",
                new XElement("groupId", groupId),
                new XElement("artifactId", artifactId),
                new XElement("versioning",
                    new XElement("latest", latest),
                    new XElement("release", latest),
                    new XElement("versions",
                        versions.Select(v => new XElement("version", v))),
                    new XElement("lastUpdated", timestamp))));

        using var ms = new MemoryStream();
        using (var xw = XmlWriter.Create(ms, new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false,
            Encoding = new System.Text.UTF8Encoding(false)
        }))
        {
            doc.WriteTo(xw);
        }

        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
