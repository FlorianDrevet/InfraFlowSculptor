using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;

namespace InfraFlowSculptor.Engine.Tests.Links;

public sealed class LinkKindRegistryTests
{
    [Fact]
    public void CreatePilotRegistersTheNineJ0LinkKindsOnly()
    {
        var registry = new LinkKindRegistry(PilotLinkKindHandlers.Create());

        Assert.Equal(9, registry.Keys.Count);
        Assert.True(registry.Supports(LinkKind.ImagePull));
        Assert.False(registry.Supports(LinkKind.HostStorage));
        Assert.False(registry.Supports(LinkKind.ConfigurationRead));
    }

    [Fact]
    public void ImagePullRequiresIdentityAndDiagnosticsCreateDeploymentDependency()
    {
        var registry = new LinkKindRegistry(PilotLinkKindHandlers.Create());

        Assert.Equal(LinkIdentityRequirement.UserAssigned, registry.Get(LinkKind.ImagePull).IdentityRequirement);
        Assert.True(registry.Get(LinkKind.Diagnostics).CreatesDeploymentDependency);
        Assert.True(registry.Get(LinkKind.Logging).CreatesDeploymentDependency);
    }

    [Fact]
    public void DiagnosticsUsesCapabilityInsteadOfAsSourceLinkDeclaration()
    {
        var catalog = CatalogLoader.Load().LatestPublished!;
        var handler = new LinkKindRegistry(PilotLinkKindHandlers.Create()).Get(LinkKind.Diagnostics);

        Assert.True(handler.Supports(
            catalog.GetResourceType("ContainerApp")!,
            catalog.GetResourceType("LogAnalyticsWorkspace")!));
    }

    [Fact]
    public void SecretAndValueDependencyLinksCannotBeCreatedExplicitly()
    {
        var registry = new LinkKindRegistry(PilotLinkKindHandlers.Create());

        Assert.True(registry.Get(LinkKind.SecretRead).IsImplicitOnly);
        Assert.True(registry.Get(LinkKind.ValueDependency).IsImplicitOnly);
        Assert.False(registry.Get(LinkKind.ImagePull).IsImplicitOnly);
    }

    [Fact]
    public void RequiredCardinalityComesFromCatalogAndOptionalMaximumComesFromHandler()
    {
        var catalog = CatalogLoader.Load().LatestPublished!;
        var logging = new LinkKindRegistry(PilotLinkKindHandlers.Create()).Get(LinkKind.Logging);

        Assert.Equal(new LinkCardinality(1, 1), logging.GetCardinality(catalog.GetResourceType("ApplicationInsights")!));
        Assert.Equal(new LinkCardinality(0, 1), logging.GetCardinality(catalog.GetResourceType("ContainerAppsEnvironment")!));
    }

    [Fact]
    public void AccessToLogAnalyticsIsAllowedByPatchWhilePinnedPilotRemainsUnchanged()
    {
        var catalog = CatalogLoader.Load();
        var access = new LinkKindRegistry(PilotLinkKindHandlers.Create()).Get(LinkKind.Access);
        var original = catalog.GetVersion("2026.10-pilot");
        var patch = catalog.GetVersion("2026.10-pilot.1");
        Assert.NotNull(original);
        Assert.NotNull(patch);

        Assert.False(access.Supports(
            original.GetResourceType("ContainerApp")!,
            original.GetResourceType("LogAnalyticsWorkspace")!));
        Assert.True(access.Supports(
            patch.GetResourceType("ContainerApp")!,
            patch.GetResourceType("LogAnalyticsWorkspace")!));
    }
}
