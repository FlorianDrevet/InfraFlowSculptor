using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Validation.Constraints;

namespace InfraFlowSculptor.Engine.Tests.Validation;

public sealed class ResourceConstraintEvaluatorTests
{
    [Fact]
    public void ConstraintEvaluatorRegistryMatchesTheClosedCatalogSchema()
    {
        var catalogAssembly = typeof(CatalogLoader).Assembly;
        var schemaResource = Assert.Single(catalogAssembly.GetManifestResourceNames(), resourceName =>
            resourceName.EndsWith("resource-type.schema.json", StringComparison.Ordinal));
        using var stream = catalogAssembly.GetManifestResourceStream(schemaResource);
        Assert.NotNull(stream);
        using var schema = JsonDocument.Parse(stream);
        var schemaKeys = schema.RootElement.GetProperty("$defs")
            .GetProperty("constraint")
            .GetProperty("oneOf")
            .EnumerateArray()
            .Select(constraint => constraint.GetProperty("properties").GetProperty("kind").GetProperty("const").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var registryKeys = ResourceConstraintEvaluatorRegistry.CreateDefault().Keys
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(schemaKeys, registryKeys);
    }

    [Fact]
    public void CompareEvaluatorRejectsAnUnsupportedOperator()
    {
        using var constraint = JsonDocument.Parse("""
            {"id":"range","kind":"compare","left":"min","operator":"greaterThan","right":"max"}
            """);
        using var minimum = JsonDocument.Parse("1");
        using var maximum = JsonDocument.Parse("2");
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["min"] = minimum.RootElement.Clone(),
            ["max"] = maximum.RootElement.Clone()
        };

        var failure = new CompareResourceConstraintEvaluator().GetFailure(constraint.RootElement, values);

        Assert.NotNull(failure);
        Assert.Contains("operator", failure.English, StringComparison.Ordinal);
        Assert.Contains("greaterThan", failure.English, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowedTuplesTreatNumericallyEquivalentJsonNumbersAsEqual()
    {
        using var constraint = JsonDocument.Parse("""
            {
              "id": "same-number",
              "kind": "allowedTuples",
              "properties": ["cpu"],
              "tuples": [[1]]
            }
            """);
        using var actualValue = JsonDocument.Parse("1.0");
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["cpu"] = actualValue.RootElement.Clone()
        };

        var failure = new AllowedTuplesResourceConstraintEvaluator().GetFailure(constraint.RootElement, values);

        Assert.Null(failure);
    }
}
