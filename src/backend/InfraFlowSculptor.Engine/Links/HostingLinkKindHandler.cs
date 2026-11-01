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
    private const string SqlDatabaseType = "SqlDatabase";
    private const string SqlServerType = "SqlServer";

    public override bool IsResourceHostingRelationship => true;

    public override bool RequiresSameSubscriptionAndRegion => true;

    public override int? MaximumFor(CatalogResourceTypeDescriptor source) => 1;

    public override bool SupportsComponentScope(
        CatalogResourceTypeDescriptor source,
        CatalogResourceTypeDescriptor target,
        Guid sourceComponentId,
        Guid targetComponentId) =>
        !string.Equals(source.Type, SqlDatabaseType, StringComparison.Ordinal)
        || !string.Equals(target.Type, SqlServerType, StringComparison.Ordinal)
        || sourceComponentId == targetComponentId;

    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target) =>
        (source.Type == "ContainerApp" && target.Type == "ContainerAppsEnvironment")
        || (source.Type == "SqlDatabase" && target.Type == "SqlServer");
}
