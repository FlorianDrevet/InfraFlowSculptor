using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed class HostingLinkKindHandler() : LinkKindHandler(
    LinkKind.Hosting,
    labelFr: "Hébergement",
    labelEn: "Hosting",
    isImplicitOnly: false,
    createsDeploymentDependency: true,
    LinkIdentityRequirement.None,
    new LinkFormDefinition([]))
{
    public override int? MaximumFor(CatalogResourceTypeDescriptor source) => 1;

    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target) =>
        (source.Type == "ContainerApp" && target.Type == "ContainerAppsEnvironment")
        || (source.Type == "SqlDatabase" && target.Type == "SqlServer");
}
