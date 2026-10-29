using System.Text.Json;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Ordering;

namespace InfraFlowSculptor.Engine.Tests.Ordering;

public sealed class ComponentOrderingTests
{
    private static readonly CatalogVersion Catalog = CatalogLoader.Load().GetVersion("2026.10-pilot")!;

    [Fact]
    public void OrdersPilotComponentsByDependenciesAndCode()
    {
        var coreId = Guid.NewGuid();
        var dataId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var ordersId = Guid.NewGuid();
        var coreWorkspace = Resource("LogAnalyticsWorkspace");
        var dataWorkspace = Resource("LogAnalyticsWorkspace");
        var dataInsights = Resource("ApplicationInsights");
        var platformWorkspace = Resource("LogAnalyticsWorkspace");
        var ordersCoreInsights = Resource("ApplicationInsights");
        var ordersDataInsights = Resource("ApplicationInsights");
        var ordersPlatformInsights = Resource("ApplicationInsights");
        var components = new[]
        {
            Component(coreId, "core", coreWorkspace),
            Component(dataId, "data", dataWorkspace, dataInsights),
            Component(platformId, "platform", platformWorkspace),
            Component(ordersId, "orders", ordersCoreInsights, ordersDataInsights, ordersPlatformInsights)
        };
        var links = new[]
        {
            Logging(dataId, dataInsights.Id, coreId, coreWorkspace.Id),
            Logging(ordersId, ordersCoreInsights.Id, coreId, coreWorkspace.Id),
            Logging(ordersId, ordersDataInsights.Id, dataId, dataWorkspace.Id),
            Logging(ordersId, ordersPlatformInsights.Id, platformId, platformWorkspace.Id)
        };

        var result = CreateOrdering().Order(Snapshot(components, links), Catalog);

        Assert.Equal(["core", "data", "platform", "orders"], result.OrderedComponents.Select(component => component.Code));
        Assert.Empty(result.Cycles);
    }

    [Fact]
    public void ReportsCycleWithTheLinksThatCreateIt()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstWorkspace = Resource("LogAnalyticsWorkspace");
        var firstInsights = Resource("ApplicationInsights");
        var secondWorkspace = Resource("LogAnalyticsWorkspace");
        var secondInsights = Resource("ApplicationInsights");
        var firstLink = Logging(firstId, firstInsights.Id, secondId, secondWorkspace.Id);
        var secondLink = Logging(secondId, secondInsights.Id, firstId, firstWorkspace.Id);

        var result = CreateOrdering().Order(Snapshot(
            [Component(firstId, "first", firstWorkspace, firstInsights), Component(secondId, "second", secondWorkspace, secondInsights)],
            [firstLink, secondLink]), Catalog);

        var cycle = Assert.Single(result.Cycles);
        Assert.Equal(2, cycle.ComponentIds.Count);
        Assert.Equal(new[] { firstLink.Id, secondLink.Id }.Order(), cycle.Dependencies.Select(dependency => dependency.LinkId).Order());
    }

    [Fact]
    public void KeepsEveryComponentInTheResultWhenAThreeComponentCyclePreventsTopologicalOrdering()
    {
        var alphaId = Guid.NewGuid();
        var betaId = Guid.NewGuid();
        var gammaId = Guid.NewGuid();
        var alphaWorkspace = Resource("LogAnalyticsWorkspace");
        var alphaInsights = Resource("ApplicationInsights");
        var betaWorkspace = Resource("LogAnalyticsWorkspace");
        var betaInsights = Resource("ApplicationInsights");
        var gammaWorkspace = Resource("LogAnalyticsWorkspace");
        var gammaInsights = Resource("ApplicationInsights");
        var components = new[]
        {
            Component(alphaId, "alpha", alphaWorkspace, alphaInsights),
            Component(betaId, "beta", betaWorkspace, betaInsights),
            Component(gammaId, "gamma", gammaWorkspace, gammaInsights)
        };
        var links = new[]
        {
            Logging(alphaId, alphaInsights.Id, betaId, betaWorkspace.Id),
            Logging(betaId, betaInsights.Id, gammaId, gammaWorkspace.Id),
            Logging(gammaId, gammaInsights.Id, alphaId, alphaWorkspace.Id)
        };

        var result = CreateOrdering().Order(Snapshot(components, links), Catalog);

        Assert.Equal(["alpha", "beta", "gamma"], result.OrderedComponents.Select(component => component.Code));
        Assert.Single(result.Cycles);
    }

    [Fact]
    public void KeepsDownstreamComponentsAfterTheirCycleDependencies()
    {
        var alphaId = Guid.NewGuid();
        var betaId = Guid.NewGuid();
        var gammaId = Guid.NewGuid();
        var alphaWorkspace = Resource("LogAnalyticsWorkspace");
        var alphaInsights = Resource("ApplicationInsights");
        var betaWorkspace = Resource("LogAnalyticsWorkspace");
        var betaInsights = Resource("ApplicationInsights");
        var gammaWorkspace = Resource("LogAnalyticsWorkspace");
        var gammaInsights = Resource("ApplicationInsights");
        var components = new[]
        {
            Component(alphaId, "alpha", alphaWorkspace, alphaInsights),
            Component(betaId, "beta", betaWorkspace, betaInsights),
            Component(gammaId, "gamma", gammaWorkspace, gammaInsights)
        };
        var links = new[]
        {
            Logging(alphaId, alphaInsights.Id, gammaId, gammaWorkspace.Id),
            Logging(betaId, betaInsights.Id, gammaId, gammaWorkspace.Id),
            Logging(gammaId, gammaInsights.Id, betaId, betaWorkspace.Id)
        };

        var result = CreateOrdering().Order(Snapshot(components, links), Catalog);

        Assert.Equal(["beta", "gamma", "alpha"], result.OrderedComponents.Select(component => component.Code));
        Assert.Single(result.Cycles);
    }

    [Fact]
    public void UsesCodeOrderWhenComponentsHaveNoDependencies()
    {
        var result = CreateOrdering().Order(Snapshot(
        [
            Component(Guid.NewGuid(), "zeta", Resource("LogAnalyticsWorkspace")),
            Component(Guid.NewGuid(), "alpha", Resource("LogAnalyticsWorkspace")),
            Component(Guid.NewGuid(), "middle", Resource("LogAnalyticsWorkspace"))
        ], []), Catalog);

        Assert.Equal(["alpha", "middle", "zeta"], result.OrderedComponents.Select(component => component.Code));
    }

    private static ComponentOrdering CreateOrdering() => new(new WiringEngine(
        new LinkKindRegistry(PilotLinkKindHandlers.Create()),
        new DataAccessProviderRegistry([new AzureSqlDataAccessProvider()])));

    private static ModelSnapshot Snapshot(
        IReadOnlyList<ComponentSnapshot> components,
        IReadOnlyList<LinkSnapshot> links) => new(
        new ProjectSnapshot("Project", "project", string.Empty),
        [],
        new NamingConventionSnapshot("Compact", "{name}", "rg-{name}"),
        new Dictionary<string, string>(),
        components)
    {
        Links = links
    };

    private static ComponentSnapshot Component(Guid id, string code, params ResourceSnapshot[] resources) => new(
        id,
        code,
        code,
        "PerEnvironment",
        [],
        null,
        resources.Select(resource => resource.ResourceGroupId).Distinct()
            .Select(groupId => new ResourceGroupSnapshot(groupId, "group", null, new Dictionary<string, string>(), new Dictionary<Guid, string>()))
            .ToArray(),
        resources);

    private static ResourceSnapshot Resource(string type) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        type,
        type.ToLowerInvariant(),
        string.Empty,
        false,
        new Dictionary<string, JsonElement>(),
        new Dictionary<Guid, IReadOnlyDictionary<string, JsonElement>>(),
        new Dictionary<Guid, bool>(),
        new Dictionary<Guid, string>(),
        new Dictionary<Guid, string>(),
        new Dictionary<string, string>(),
        []);

    private static LinkSnapshot Logging(Guid sourceComponentId, Guid sourceResourceId, Guid targetComponentId, Guid targetResourceId) => new(
        Guid.NewGuid(),
        sourceComponentId,
        sourceResourceId,
        targetComponentId,
        targetResourceId,
        LinkKind.Logging,
        LinkOrigin.Explicit,
        LinkIdentityKind.None,
        null,
        [],
        LinkAccessScope.Resource,
        null);
}
