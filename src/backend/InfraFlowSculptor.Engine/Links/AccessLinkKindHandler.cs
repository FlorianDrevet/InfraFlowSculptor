using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed class AccessLinkKindHandler() : LinkKindHandler(
    LinkKind.Access,
    labelFr: "Accès",
    labelEn: "Access",
    isImplicitOnly: false,
    createsDeploymentDependency: true,
    LinkIdentityRequirement.Required,
    new LinkFormDefinition([
        new LinkFormField("identity", "identity", true),
        new LinkFormField("roles", "roles", true),
        new LinkFormField("scope", "scope", true)
    ]))
{
    public override bool SourceActsAsIdentity(CatalogResourceTypeDescriptor source) => source.Type == "UserAssignedIdentity";

    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target)
    {
        var sourceIdentities = source.Definition.GetProperty("identities");
        var supportsIdentity = sourceIdentities.GetProperty("system").GetBoolean()
            || sourceIdentities.GetProperty("userAssignedMax").GetInt32() > 0;
        return supportsIdentity || source.Type == "UserAssignedIdentity";
    }

    public override IReadOnlyList<string> GetRequiredRoles(
        LinkSnapshot link,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> targetProperties,
        CatalogResourceTypeDescriptor targetDescriptor) =>
        link.Roles;
}
