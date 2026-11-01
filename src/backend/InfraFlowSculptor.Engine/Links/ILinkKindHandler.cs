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

    bool IsResourceHostingRelationship { get; }

    bool ProducesDiagnosticSettings { get; }

    bool IsImagePullRelationship { get; }

    bool CreatesDeploymentDependency { get; }

    bool RequiresSameSubscriptionAndRegion { get; }

    bool CreatesDeploymentDependencyFor(
        LinkSnapshot link,
        ModelSnapshot snapshot,
        CatalogResourceTypeDescriptor source,
        CatalogResourceTypeDescriptor target);

    LinkIdentityRequirement IdentityRequirement { get; }

    LinkFormDefinition Form { get; }

    string? RoleEquivalenceKey { get; }

    bool SourceActsAsIdentity(CatalogResourceTypeDescriptor source);

    LinkCardinality GetCardinality(CatalogResourceTypeDescriptor source);

    bool Supports(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target);

    bool SupportsComponentScope(
        CatalogResourceTypeDescriptor source,
        CatalogResourceTypeDescriptor target,
        Guid sourceComponentId,
        Guid targetComponentId);

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
