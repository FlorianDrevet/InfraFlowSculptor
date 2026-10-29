using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Links;

public interface ILinkKindHandler : IKeyed<LinkKind>
{
    string LabelFr { get; }

    string LabelEn { get; }

    bool IsImplicitOnly { get; }

    bool CreatesDeploymentDependency { get; }

    LinkIdentityRequirement IdentityRequirement { get; }

    LinkFormDefinition Form { get; }

    string? RoleEquivalenceKey { get; }

    bool SourceActsAsIdentity(CatalogResourceTypeDescriptor source);

    LinkCardinality GetCardinality(CatalogResourceTypeDescriptor source);

    bool Supports(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target);

    IReadOnlyList<LinkSnapshot> GetImplicitLinks(ModelSnapshot snapshot, CatalogVersion catalog);

    IReadOnlyList<ImplicitApplicationSetting> GetImplicitApplicationSettings(
        ModelSnapshot snapshot,
        CatalogVersion catalog,
        IReadOnlyList<LinkSnapshot> links);

    LinkIdentityRequirement GetIdentityRequirement(IReadOnlyDictionary<string, System.Text.Json.JsonElement> targetProperties);

    IReadOnlyList<string> GetRequiredRoles(
        LinkSnapshot link,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> targetProperties,
        CatalogResourceTypeDescriptor targetDescriptor);
}
