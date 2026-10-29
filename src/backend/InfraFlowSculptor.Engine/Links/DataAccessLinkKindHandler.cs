using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed class DataAccessLinkKindHandler() : LinkKindHandler(
    LinkKind.DataAccess,
    labelFr: "Accès aux données",
    labelEn: "Data access",
    isImplicitOnly: false,
    createsDeploymentDependency: true,
    LinkIdentityRequirement.Required,
    new LinkFormDefinition([
        new LinkFormField("identity", "identity", true),
        new LinkFormField("level", "dataAccessLevel", true)
    ]))
{
    public override bool SourceActsAsIdentity(CatalogResourceTypeDescriptor source) => source.Type == "UserAssignedIdentity";

    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target)
    {
        var dataAccess = target.Definition.GetProperty("dataAccess");
        return dataAccess.GetProperty("supported").GetBoolean()
            && (source.Type == "ContainerApp" || source.Type == "UserAssignedIdentity");
    }
}
