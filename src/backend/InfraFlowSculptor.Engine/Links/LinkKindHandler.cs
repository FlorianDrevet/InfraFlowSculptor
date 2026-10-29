using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Links;

public abstract class LinkKindHandler(
    LinkKind key,
    string labelFr,
    string labelEn,
    bool isImplicitOnly,
    bool createsDeploymentDependency,
    LinkIdentityRequirement identityRequirement,
    LinkFormDefinition form) : ILinkKindHandler
{
    public LinkKind Key { get; } = key;

    public string LabelFr { get; } = labelFr;

    public string LabelEn { get; } = labelEn;

    public bool IsImplicitOnly { get; } = isImplicitOnly;

    public virtual bool IsResourceHostingRelationship => false;

    public bool CreatesDeploymentDependency { get; } = createsDeploymentDependency;

    public virtual bool RequiresSameSubscriptionAndRegion => false;

    public virtual bool CreatesDeploymentDependencyFor(
        LinkSnapshot link,
        ModelSnapshot snapshot,
        CatalogResourceTypeDescriptor source,
        CatalogResourceTypeDescriptor target) => CreatesDeploymentDependency;

    public LinkIdentityRequirement IdentityRequirement { get; } = identityRequirement;

    public LinkFormDefinition Form { get; } = form;

    public virtual string? RoleEquivalenceKey => null;

    public virtual bool SourceActsAsIdentity(CatalogResourceTypeDescriptor source) => false;

    public virtual int? MaximumFor(CatalogResourceTypeDescriptor source) => null;

    public LinkCardinality GetCardinality(CatalogResourceTypeDescriptor source)
    {
        var minimum = 0;
        int? maximum = null;
        foreach (var requirement in source.Definition.GetProperty("links").GetProperty("required").EnumerateArray())
        {
            if (!string.Equals(requirement.GetProperty("kind").GetString(), Key.ToString(), StringComparison.Ordinal))
            {
                continue;
            }

            minimum = 1;
            if (string.Equals(requirement.GetProperty("cardinality").GetString(), "one", StringComparison.Ordinal))
            {
                maximum = 1;
            }
        }

        var handlerMaximum = MaximumFor(source);
        if (handlerMaximum is not null)
        {
            maximum = maximum is null ? handlerMaximum : Math.Min(maximum.Value, handlerMaximum.Value);
        }

        return new LinkCardinality(minimum, maximum);
    }

    public virtual IReadOnlyList<LinkSnapshot> GetImplicitLinks(ModelSnapshot snapshot, CatalogVersion catalog) => [];

    public virtual IReadOnlyList<ImplicitApplicationSetting> GetImplicitApplicationSettings(
        ModelSnapshot snapshot,
        CatalogVersion catalog,
        IReadOnlyList<LinkSnapshot> links) => [];

    public virtual LinkIdentityRequirement GetIdentityRequirement(
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> targetProperties) => IdentityRequirement;

    public bool Supports(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target) =>
        SupportsSource(source)
        && SupportsTarget(target)
        && SupportsTypes(source, target);

    protected virtual bool SupportsSource(CatalogResourceTypeDescriptor source) =>
        source.Definition.GetProperty("links").GetProperty("asSource")
            .EnumerateArray()
            .Any(kind => string.Equals(kind.GetString(), Key.ToString(), StringComparison.Ordinal));

    protected virtual bool SupportsTarget(CatalogResourceTypeDescriptor target) =>
        target.Definition.GetProperty("links").GetProperty("asTarget")
            .EnumerateArray()
            .Any(kind => string.Equals(kind.GetString(), Key.ToString(), StringComparison.Ordinal));

    public virtual IReadOnlyList<string> GetRequiredRoles(
        LinkSnapshot link,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> targetProperties,
        CatalogResourceTypeDescriptor targetDescriptor) => [];

    protected abstract bool SupportsTypes(CatalogResourceTypeDescriptor source, CatalogResourceTypeDescriptor target);
}
