using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Plan;

namespace InfraFlowSculptor.Engine.Tests.Plan;

public sealed class DeploymentPlanJsonSerializerTests
{
    [Fact]
    public void CanonicalSerializationSortsObjectPropertiesAndDictionaries()
    {
        var first = DeploymentPlanJsonSerializer.Serialize(CreatePlan(
            new Dictionary<string, CatalogModulePin>(StringComparer.Ordinal)
            {
                ["z-module"] = new("br/public:z:1", "1", "source-z"),
                ["a-module"] = new("br/public:a:1", "1", "source-a")
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["z-module"] = "1.0.0",
                ["a-module"] = "2.0.0"
            }));
        var second = DeploymentPlanJsonSerializer.Serialize(CreatePlan(
            new Dictionary<string, CatalogModulePin>(StringComparer.Ordinal)
            {
                ["a-module"] = new("br/public:a:1", "1", "source-a"),
                ["z-module"] = new("br/public:z:1", "1", "source-z")
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["a-module"] = "2.0.0",
                ["z-module"] = "1.0.0"
            }));

        Assert.Equal(first, second);
        Assert.StartsWith("{\"components\":[],\"pins\":", first, StringComparison.Ordinal);
        Assert.True(first.IndexOf("\"a-module\"", StringComparison.Ordinal)
            < first.IndexOf("\"z-module\"", StringComparison.Ordinal));
    }

    private static DeploymentPlan CreatePlan(
        IReadOnlyDictionary<string, CatalogModulePin> modules,
        IReadOnlyDictionary<string, string> powerShellModules) => new(
        new ProjectInfo("Shop", "shop", "Pilot", "Bicep", "AzureDevOps", "AVM registre public", null),
        [],
        new CatalogPins(
            "2026.10-pilot.5",
            "fingerprint",
            "0.47.16",
            new CatalogStartupImagePin("image@sha256:abc", "source"),
            modules,
            powerShellModules,
            new CatalogPipelineValidationPins(
                new CatalogPipelineSchemaPin("repository", "commit", "path", "hash", "source"),
                "0.74.0"),
            new Dictionary<string, CatalogNativeResourcePin>(StringComparer.Ordinal)
            {
                ["roleAssignment"] = new("Microsoft.Authorization/roleAssignments", "2022-04-01", "reason")
            }));
}
