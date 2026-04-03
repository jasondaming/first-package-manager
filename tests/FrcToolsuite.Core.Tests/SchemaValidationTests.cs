using System.Text.Json;
using Json.Schema;

namespace FrcToolsuite.Core.Tests;

public class SchemaValidationTests
{
    private static readonly string RegistryRoot = FindRegistryRoot();

    private static string FindRegistryRoot()
    {
        // Walk up from the test assembly location to find the registry directory
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var registryDir = Path.Combine(dir, "registry");
            if (Directory.Exists(registryDir) && File.Exists(Path.Combine(registryDir, "index.json")))
            {
                return registryDir;
            }
            dir = Path.GetDirectoryName(dir);
        }

        // Fallback: try the repo root relative to test project
        var fallback = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "registry"));
        if (Directory.Exists(fallback))
        {
            return fallback;
        }

        throw new DirectoryNotFoundException("Could not locate the registry directory.");
    }

    private static JsonSchema LoadSchema(string schemaFileName)
    {
        var schemaPath = Path.Combine(RegistryRoot, "schemas", schemaFileName);
        var schemaJson = File.ReadAllText(schemaPath);
        return JsonSchema.FromText(schemaJson);
    }

    private static JsonDocument LoadJson(string relativePath)
    {
        var fullPath = Path.Combine(RegistryRoot, relativePath);
        var json = File.ReadAllText(fullPath);
        return JsonDocument.Parse(json);
    }

    [Fact]
    public void RegistryIndex_IsValidAgainstSchema()
    {
        var schema = LoadSchema("registry-index.v1.schema.json");
        using var doc = LoadJson("index.json");

        var result = schema.Evaluate(doc.RootElement);

        Assert.True(result.IsValid, FormatErrors(result));
    }

    [Fact]
    public void PackageManifest_JdkIsValidAgainstSchema()
    {
        var schema = LoadSchema("package-manifest.v1.schema.json");
        using var doc = LoadJson(Path.Combine("packages", "wpilib", "jdk-2026-17.0.16.json"));

        var result = schema.Evaluate(doc.RootElement);

        Assert.True(result.IsValid, FormatErrors(result));
    }

    [Fact]
    public void BundleDefinition_FrcJavaStarterIsValidAgainstSchema()
    {
        var schema = LoadSchema("bundle.v1.schema.json");
        using var doc = LoadJson(Path.Combine("bundles", "frc-java-starter.json"));

        var result = schema.Evaluate(doc.RootElement);

        Assert.True(result.IsValid, FormatErrors(result));
    }

    [Fact]
    public void PackageManifest_MissingRequiredField_FailsValidation()
    {
        var schema = LoadSchema("package-manifest.v1.schema.json");

        // Create an invalid manifest missing required fields (no "artifacts", "category", etc.)
        var invalidJson = """
            {
                "id": "test.package",
                "name": "Test Package",
                "version": "1.0.0"
            }
            """;

        using var doc = JsonDocument.Parse(invalidJson);
        var result = schema.Evaluate(doc.RootElement);

        Assert.False(result.IsValid, "Expected validation to fail for manifest missing required fields.");
    }

    [Fact]
    public void RegistryIndex_MissingPackages_FailsValidation()
    {
        var schema = LoadSchema("registry-index.v1.schema.json");

        var invalidJson = """
            {
                "schemaVersion": "1.0",
                "lastUpdated": "2026-01-01T00:00:00Z"
            }
            """;

        using var doc = JsonDocument.Parse(invalidJson);
        var result = schema.Evaluate(doc.RootElement);

        Assert.False(result.IsValid, "Expected validation to fail for index missing 'packages'.");
    }

    [Fact]
    public void BundleDefinition_MissingPackages_FailsValidation()
    {
        var schema = LoadSchema("bundle.v1.schema.json");

        var invalidJson = """
            {
                "id": "test-bundle",
                "name": "Test Bundle",
                "season": 2026,
                "description": "A test bundle.",
                "competition": "Frc"
            }
            """;

        using var doc = JsonDocument.Parse(invalidJson);
        var result = schema.Evaluate(doc.RootElement);

        Assert.False(result.IsValid, "Expected validation to fail for bundle missing 'packages'.");
    }

    [Fact]
    public void TeamProfile_ValidDocument_PassesValidation()
    {
        var schema = LoadSchema("team-profile.v1.schema.json");

        var validJson = """
            {
                "schemaVersion": "1.0",
                "profileName": "Team 254 Dev Setup",
                "teamNumber": 254,
                "competition": "Frc",
                "season": 2026,
                "createdAt": "2026-01-15T10:00:00Z",
                "packages": ["wpilib.jdk", "wpilib.vscode"],
                "notes": "Standard development environment."
            }
            """;

        using var doc = JsonDocument.Parse(validJson);
        var result = schema.Evaluate(doc.RootElement);

        Assert.True(result.IsValid, FormatErrors(result));
    }

    [Fact]
    public void TeamProfile_MissingRequired_FailsValidation()
    {
        var schema = LoadSchema("team-profile.v1.schema.json");

        var invalidJson = """
            {
                "schemaVersion": "1.0",
                "profileName": "Incomplete Profile"
            }
            """;

        using var doc = JsonDocument.Parse(invalidJson);
        var result = schema.Evaluate(doc.RootElement);

        Assert.False(result.IsValid, "Expected validation to fail for profile missing required fields.");
    }

    private static string FormatErrors(EvaluationResults results)
    {
        if (results.IsValid)
        {
            return string.Empty;
        }

        var errors = new List<string>();
        CollectErrors(results, errors);
        return $"Schema validation failed:\n{string.Join("\n", errors)}";
    }

    private static void CollectErrors(EvaluationResults results, List<string> errors)
    {
        if (results.Errors != null)
        {
            foreach (var error in results.Errors)
            {
                errors.Add($"  [{results.InstanceLocation}] {error.Key}: {error.Value}");
            }
        }

        if (results.Details != null)
        {
            foreach (var detail in results.Details)
            {
                CollectErrors(detail, errors);
            }
        }
    }
}
