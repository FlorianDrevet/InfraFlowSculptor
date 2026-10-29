using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Exposure;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Tests.Exposure;

public sealed class ExposureResolverTests
{
    private static readonly CatalogVersion ExposureCatalog = CatalogLoader.Load().GetVersion("2026.10-pilot.2")!;
    private static readonly string[] BaseRanges = ["10.0.0.0/8"];
    private static readonly string[] DevRanges = ["10.1.0.0/16"];

    [Fact]
    public void UsesTheCatalogDefaultForSupportedResources()
    {
        var resolver = CreateResolver();
        var resource = CreateResource();

        var exposure = resolver.Resolve(
            resource,
            Guid.NewGuid(),
            ExposureCatalog.GetResourceType("KeyVault")!);

        Assert.IsType<PublicExposure>(exposure);
    }

    [Fact]
    public void ResolvesExposureAndRangesIndependentlyForEachEnvironment()
    {
        var devId = Guid.NewGuid();
        var prdId = Guid.NewGuid();
        var resource = CreateResource(
            Values(("exposure", "Restricted"), ("allowedIpRanges", BaseRanges)),
            new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>
            {
                [devId] = Values(("exposure", "Restricted"), ("allowedIpRanges", DevRanges)),
                [prdId] = Values(("exposure", "Public"))
            });
        var resolver = CreateResolver();
        var descriptor = ExposureCatalog.GetResourceType("KeyVault")!;

        var devExposure = Assert.IsType<RestrictedExposure>(resolver.Resolve(resource, devId, descriptor));
        var prdExposure = resolver.Resolve(resource, prdId, descriptor);

        Assert.Equal(DevRanges, devExposure.Ranges);
        Assert.IsType<PublicExposure>(prdExposure);
    }

    [Fact]
    public void PreservesEmptyRestrictedRangesForTheJ020Finding()
    {
        var targetId = Guid.NewGuid();
        var resource = CreateResource(overrides: new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>
        {
            [targetId] = Values(("exposure", "Restricted"))
        });

        var exposure = Assert.IsType<RestrictedExposure>(CreateResolver().Resolve(
            resource,
            targetId,
            ExposureCatalog.GetResourceType("KeyVault")!));

        Assert.Empty(exposure.Ranges);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public void RejectsNumericExposureModeValues(string mode)
    {
        var resource = CreateResource(Values(("exposure", mode)));

        Assert.Throws<InvalidOperationException>(() => CreateResolver().Resolve(
            resource,
            Guid.NewGuid(),
            ExposureCatalog.GetResourceType("KeyVault")!));
    }

    [Fact]
    public void DoesNotProjectExposureForUnsupportedResourceTypes()
    {
        var exposure = CreateResolver().Resolve(
            CreateResource(type: "ApplicationInsights"),
            Guid.NewGuid(),
            ExposureCatalog.GetResourceType("ApplicationInsights")!);

        Assert.Null(exposure);
    }

    [Fact]
    public void RegistersOnlyPublicAndRestrictedExposureHandlersForThePilot()
    {
        var registry = new ExposureHandlerRegistry(PilotExposureHandlers.Create());

        Assert.Equal(2, registry.Keys.Count);
        Assert.True(registry.Supports(ExposureMode.Public));
        Assert.True(registry.Supports(ExposureMode.Restricted));
    }

    private static ExposureResolver CreateResolver() =>
        new(new ExposureHandlerRegistry(PilotExposureHandlers.Create()));

    private static ResourceSnapshot CreateResource(
        IReadOnlyDictionary<string, JsonElement>? properties = null,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, JsonElement>>? overrides = null,
        string type = "KeyVault") => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            type,
            "main",
            string.Empty,
            false,
            properties ?? new Dictionary<string, JsonElement>(),
            overrides ?? new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(),
            new Dictionary<Guid, bool>(),
            new Dictionary<Guid, string>(),
            new Dictionary<Guid, string>(),
            new Dictionary<string, string>(),
            []);

    private static Dictionary<string, JsonElement> Values(params (string Name, object Value)[] values) =>
        values.ToDictionary(
            value => value.Name,
            value => JsonSerializer.SerializeToElement(value.Value),
            StringComparer.Ordinal);
}
