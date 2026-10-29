using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed class ValueDependencyLinkKindHandler() : LinkKindHandler(
    LinkKind.ValueDependency,
    isImplicitOnly: true,
    createsDeploymentDependency: true,
    LinkIdentityRequirement.None,
    new LinkFormDefinition([]))
{
    protected override bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target) =>
        target.Definition.GetProperty("outputs").GetArrayLength() > 0;
}
