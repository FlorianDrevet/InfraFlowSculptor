using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed class SecretReadLinkKindHandler() : LinkKindHandler(
    LinkKind.SecretRead,
    isImplicitOnly: true,
    createsDeploymentDependency: true,
    LinkIdentityRequirement.Required,
    new LinkFormDefinition([new LinkFormField("identity", "identity", true)]))
{
    public override string? RoleEquivalenceKey => "secretRead";

    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target) =>
        source.Type == "ContainerApp" && target.Type == "KeyVault";

    public override IReadOnlyList<string> GetRequiredRoles(
        LinkSnapshot link,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> targetProperties,
        CatalogResourceTypeDescriptor targetDescriptor) =>
        ["Key Vault Secrets User"];
}
