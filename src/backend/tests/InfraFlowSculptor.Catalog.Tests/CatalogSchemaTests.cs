using System.Security.Cryptography;
using System.Text.Json;
using InfraFlowSculptor.Catalog;
using Json.Schema;

namespace InfraFlowSculptor.Catalog.Tests;

public sealed class CatalogSchemaTests
{
    private static readonly string CatalogRoot = Path.Combine(AppContext.BaseDirectory, "catalog");
    private static readonly string PilotRoot = Path.Combine(CatalogRoot, "2026.10-pilot");

    [Fact]
    public void PublishedPilotContainsNineResourceDescriptors()
    {
        var provider = CatalogLoader.Load(typeof(CatalogLoader).Assembly);

        Assert.Equal(9, Assert.Single(provider.GetVersions()).ResourceTypes.Length);
    }

    [Fact]
    public void EveryCatalogDocumentSatisfiesItsPublishedSchema()
    {
        AssertValid("schema/catalog-version.schema.json", "2026.10-pilot/catalog.json");
        AssertValid("schema/regions.schema.json", "2026.10-pilot/regions.json");
        AssertValid("schema/role-catalog.schema.json", "2026.10-pilot/roles.json");

        foreach (var descriptorPath in Directory.GetFiles(Path.Combine(PilotRoot, "types"), "*.json"))
        {
            AssertValid("schema/resource-type.schema.json", Path.GetRelativePath(CatalogRoot, descriptorPath));
        }

        foreach (var stepPath in Directory.GetFiles(Path.Combine(PilotRoot, "steps"), "*.json"))
        {
            AssertValid("schema/step.schema.json", Path.GetRelativePath(CatalogRoot, stepPath));
        }
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
        using var rolesDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(PilotRoot, "roles.json")));
        var roleNames = rolesDocument.RootElement.GetProperty("roles")
            .EnumerateArray()
            .Select(role => role.GetProperty("name").GetString())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var path in Directory.GetFiles(Path.Combine(PilotRoot, "types"), "*.json"))
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

    [Fact]
    public void EveryBicepMappingMatchesAParameterInItsRestoredAvmModule()
    {
        using var pins = JsonDocument.Parse(File.ReadAllText(Path.Combine(PilotRoot, "pins.json")));
        var modules = pins.RootElement.GetProperty("modules");
        var bicepCacheRoot = Environment.GetEnvironmentVariable("BICEP_CACHE_ROOT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bicep", "br", "mcr.microsoft.com");

        foreach (var descriptorPath in Directory.GetFiles(Path.Combine(PilotRoot, "types"), "*.json"))
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

    [Fact]
    public void RegionsAndPinnedAzurePipelinesSchemaMatchTheirSourceData()
    {
        using var regions = JsonDocument.Parse(File.ReadAllText(Path.Combine(PilotRoot, "regions.json")));
        var regionItems = regions.RootElement.GetProperty("regions").EnumerateArray().ToArray();
        Assert.Equal(24, regionItems.Length);
        Assert.Equal(24, regionItems.Select(region => region.GetProperty("name").GetString()).Distinct().Count());
        Assert.Equal(24, regionItems.Select(region => region.GetProperty("code").GetString()).Distinct().Count());

        using var pins = JsonDocument.Parse(File.ReadAllText(Path.Combine(PilotRoot, "pins.json")));
        var expectedHash = pins.RootElement
            .GetProperty("pipelineValidation")
            .GetProperty("azurePipelinesSchema")
            .GetProperty("sha256")
            .GetString()!;
        var actualHash = Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(Path.Combine(PilotRoot, "schemas", "azure-pipelines.json"))))
            .ToLowerInvariant();

        Assert.Equal(expectedHash, actualHash);
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
