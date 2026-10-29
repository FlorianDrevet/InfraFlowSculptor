using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed class LoggingLinkKindHandler() : LinkKindHandler(
    LinkKind.Logging,
    isImplicitOnly: false,
    createsDeploymentDependency: true,
    LinkIdentityRequirement.None,
    new LinkFormDefinition([]))
{
    public override int? MaximumFor(CatalogResourceTypeDescriptor source) => 1;

    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target) =>
        target.Type == "LogAnalyticsWorkspace"
        && (source.Type is "ApplicationInsights" or "ContainerAppsEnvironment");
}
