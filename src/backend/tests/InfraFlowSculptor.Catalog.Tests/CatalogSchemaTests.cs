using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using InfraFlowSculptor.Catalog;
using Json.Schema;

namespace InfraFlowSculptor.Catalog.Tests;

public sealed class CatalogSchemaTests
{
    private static readonly string CatalogRoot = Path.Combine(AppContext.BaseDirectory, "catalog");
    private static readonly string PilotRoot = Path.Combine(CatalogRoot, "2026.10-pilot");

    private static IEnumerable<string> CatalogVersionDirectories() => Directory.GetDirectories(CatalogRoot)
        .Where(path => File.Exists(Path.Combine(path, "catalog.json")))
        .OrderBy(path => path, StringComparer.Ordinal);

    [Fact]
    public void CatalogVersionSchemaAcceptsPatchReleaseOfPilot()
    {
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(CatalogRoot, "schema", "catalog-version.schema.json")));
        using var candidate = JsonDocument.Parse("""
            {
              "version": "2026.10-pilot.1",
              "status": "published",
              "publishedAt": "2026-10-29",
              "supportEndsAt": null,
              "notes": "Correctif du catalogue pilote.",
              "verifiedAgainstDocsOn": "2026-10-29"
            }
            """);

        var result = schema.Evaluate(candidate.RootElement);

        Assert.True(result.IsValid, result.ToString());
    }

    [Fact]
    public void LogAnalyticsAccessIsEnabledByPatchWithoutMutatingPinnedPilot()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);
        var original = provider.GetVersion("2026.10-pilot");
        var patch = provider.GetVersion("2026.10-pilot.1");
        Assert.NotNull(original);
        Assert.NotNull(patch);

        var originalTargetLinks = original.GetResourceType("LogAnalyticsWorkspace")!.Definition
            .GetProperty("links").GetProperty("asTarget").EnumerateArray();
        var patchTargetLinks = patch.GetResourceType("LogAnalyticsWorkspace")!.Definition
            .GetProperty("links").GetProperty("asTarget").EnumerateArray();

        Assert.DoesNotContain(originalTargetLinks, kind => kind.GetString() == "Access");
        Assert.Contains(patchTargetLinks, kind => kind.GetString() == "Access");
        Assert.Equal("2026.10-pilot.1", provider.LatestPublished?.Version);
    }

    [Fact]
    public void PublishedPilotVersionsContainNineResourceDescriptors()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);

        Assert.Equal(9, provider.GetVersion("2026.10-pilot")!.ResourceTypes.Length);
        Assert.Equal(9, provider.GetVersion("2026.10-pilot.1")!.ResourceTypes.Length);
    }

    [Fact]
    public void EveryCatalogDocumentSatisfiesItsPublishedSchema()
    {
        foreach (var versionDirectory in CatalogVersionDirectories())
        {
            var version = Path.GetFileName(versionDirectory);
            AssertValid("schema/catalog-version.schema.json", $"{version}/catalog.json");
            AssertValid("schema/regions.schema.json", $"{version}/regions.json");
            AssertValid("schema/role-catalog.schema.json", $"{version}/roles.json");

            foreach (var descriptorPath in Directory.GetFiles(Path.Combine(versionDirectory, "types"), "*.json"))
            {
                AssertValid("schema/resource-type.schema.json", Path.GetRelativePath(CatalogRoot, descriptorPath));
            }

            foreach (var stepPath in Directory.GetFiles(Path.Combine(versionDirectory, "steps"), "*.json"))
            {
                AssertValid("schema/step.schema.json", Path.GetRelativePath(CatalogRoot, stepPath));
            }
        }
    }

    [Fact]
    public void ResourcePropertySchemaAllowsAnOptionalPattern()
    {
        var path = Path.Combine(PilotRoot, "types", "SqlServer.json");
        var descriptor = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var collation = descriptor["children"]![0]!["properties"]!.AsArray()
            .Single(property => property!["name"]!.GetValue<string>() == "collation")!
            .AsObject();
        collation["pattern"] = "^[A-Za-z0-9_]+$";
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(CatalogRoot, "schema", "resource-type.schema.json")));
        using var candidate = JsonDocument.Parse(descriptor.ToJsonString());

        Assert.True(schema.Evaluate(candidate.RootElement).IsValid);
    }

    [Fact]
    public void EveryPublishedVersionMatchesItsPinnedFingerprint()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);
        using var hashes = JsonDocument.Parse(File.ReadAllText(Path.Combine(CatalogRoot, "published-hashes.json")));

        foreach (var version in provider.GetVersions().Where(candidate => candidate.Status == "published"))
        {
            var expected = hashes.RootElement.GetProperty(version.Version).GetString();
            Assert.Equal(expected, version.Fingerprint);
        }
    }

    [Fact]
    public void EveryResourceDescriptorRoleExistsInThePinnedRoleCatalog()
    {
        foreach (var versionDirectory in CatalogVersionDirectories())
        {
            using var rolesDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(versionDirectory, "roles.json")));
            var roleNames = rolesDocument.RootElement.GetProperty("roles")
                .EnumerateArray()
                .Select(role => role.GetProperty("name").GetString())
                .ToHashSet(StringComparer.Ordinal);

            foreach (var path in Directory.GetFiles(Path.Combine(versionDirectory, "types"), "*.json"))
            {
                using var descriptor = JsonDocument.Parse(File.ReadAllText(path));
                var directRoles = descriptor.RootElement.GetProperty("roles").EnumerateArray()
                    .Select(role => role.GetString()!);
                var equivalentRoles = descriptor.RootElement.GetProperty("roleEquivalences")
                    .EnumerateObject()
                    .SelectMany(group => group.Value.EnumerateArray())
                    .Select(role => role.GetString()!);
                foreach (var role in directRoles.Concat(equivalentRoles))
                {
                    Assert.Contains(role, roleNames);
                }
            }
        }
    }

    [Fact]
    public void EveryBicepMappingMatchesAParameterInItsRestoredAvmModule()
    {
        var bicepCacheRoot = Environment.GetEnvironmentVariable("BICEP_CACHE_ROOT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bicep", "br", "mcr.microsoft.com");

        foreach (var versionDirectory in CatalogVersionDirectories())
        {
            using var pins = JsonDocument.Parse(File.ReadAllText(Path.Combine(versionDirectory, "pins.json")));
            var modules = pins.RootElement.GetProperty("modules");

            foreach (var descriptorPath in Directory.GetFiles(Path.Combine(versionDirectory, "types"), "*.json"))
            {
                using var descriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));
                var generation = descriptor.RootElement.GetProperty("generation").GetProperty("Bicep");
                var moduleReference = generation.GetProperty("module").GetString()!;
                var versionPin = generation.GetProperty("versionPin").GetString()!;
                var module = modules.EnumerateObject()
                    .Select(property => property.Value)
                    .Single(pin => pin.GetProperty("reference").GetString() == moduleReference);
                var pinnedVersion = module.GetProperty("version").GetString()!;
                Assert.Equal($"{pinnedVersion}.0", versionPin);

                var relativeModulePath = moduleReference
                    .Replace("br/public:", string.Empty, StringComparison.Ordinal)
                    .Split(':', 2)[0]
                    .Replace('/', '$');
                var cacheModuleName = "bicep$" + relativeModulePath;
                var moduleJson = Path.Combine(bicepCacheRoot, cacheModuleName, versionPin + "$", "main.json");
                Assert.True(File.Exists(moduleJson), $"Restore the pinned module before testing: {moduleJson}");

                using var moduleDocument = JsonDocument.Parse(File.ReadAllText(moduleJson));
                var parameterNames = moduleDocument.RootElement.GetProperty("parameters")
                    .EnumerateObject()
                    .Select(parameter => parameter.Name)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var mapping in generation.GetProperty("map").EnumerateObject())
                {
                    Assert.Contains(mapping.Value.GetString()!, parameterNames);
                }
            }
        }
    }

    [Fact]
    public void RegionsAndPinnedAzurePipelinesSchemaMatchTheirSourceData()
    {
        foreach (var versionDirectory in CatalogVersionDirectories())
        {
            using var regions = JsonDocument.Parse(File.ReadAllText(Path.Combine(versionDirectory, "regions.json")));
            var regionItems = regions.RootElement.GetProperty("regions").EnumerateArray().ToArray();
            Assert.Equal(24, regionItems.Length);
            Assert.Equal(24, regionItems.Select(region => region.GetProperty("name").GetString()).Distinct().Count());
            Assert.Equal(24, regionItems.Select(region => region.GetProperty("code").GetString()).Distinct().Count());

            using var pins = JsonDocument.Parse(File.ReadAllText(Path.Combine(versionDirectory, "pins.json")));
            var expectedHash = pins.RootElement
                .GetProperty("pipelineValidation")
                .GetProperty("azurePipelinesSchema")
                .GetProperty("sha256")
                .GetString()!;
            var actualHash = Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(Path.Combine(versionDirectory, "schemas", "azure-pipelines.json"))))
                .ToLowerInvariant();

            Assert.Equal(expectedHash, actualHash);
        }
    }

    private static void AssertValid(string schemaRelativePath, string documentRelativePath)
    {
        var schemaPath = Path.Combine(CatalogRoot, schemaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var documentPath = Path.Combine(CatalogRoot, documentRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(schemaPath), $"Schema is missing: {schemaPath}");
        Assert.True(File.Exists(documentPath), $"Catalog document is missing: {documentPath}");

        var schema = JsonSchema.FromText(File.ReadAllText(schemaPath));
        using var document = JsonDocument.Parse(File.ReadAllText(documentPath));
        var result = schema.Evaluate(document.RootElement);
        Assert.True(result.IsValid, $"{documentRelativePath}: {result}");
    }
}
