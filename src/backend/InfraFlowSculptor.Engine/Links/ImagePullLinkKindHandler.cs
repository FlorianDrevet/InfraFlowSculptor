using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed class ImagePullLinkKindHandler() : LinkKindHandler(
    LinkKind.ImagePull,
    isImplicitOnly: false,
    createsDeploymentDependency: true,
    LinkIdentityRequirement.UserAssigned,
    new LinkFormDefinition([new LinkFormField("identity", "identity", true)]))
{
    public override int? MaximumFor(CatalogResourceTypeDescriptor source) => 1;

    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target) =>
        source.Type == "ContainerApp" && target.Type == "ContainerRegistry";

    public override IReadOnlyList<string> GetRequiredRoles(
        LinkSnapshot link,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> targetProperties,
        CatalogResourceTypeDescriptor targetDescriptor) => ["AcrPull"];
}
