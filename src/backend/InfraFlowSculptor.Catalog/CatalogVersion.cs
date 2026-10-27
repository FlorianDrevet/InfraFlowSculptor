using System.Collections.Immutable;

namespace InfraFlowSculptor.Catalog;

public sealed class CatalogVersion
{
    internal CatalogVersion(
        string version,
        string status,
        DateOnly publishedAt,
        DateOnly? supportEndsAt,
        string notes,
        DateOnly verifiedAgainstDocsOn,
        ImmutableArray<CatalogResourceTypeDescriptor> resourceTypes,
        ImmutableArray<CatalogRegion> regions,
        string fingerprint)
    {
        Version = version;
        Status = status;
        PublishedAt = publishedAt;
        SupportEndsAt = supportEndsAt;
        Notes = notes;
        VerifiedAgainstDocsOn = verifiedAgainstDocsOn;
        ResourceTypes = resourceTypes;
        Regions = regions;
        Fingerprint = fingerprint;
    }

    public string Version { get; }

    public string Status { get; }

    public DateOnly PublishedAt { get; }

    public DateOnly? SupportEndsAt { get; }

    public string Notes { get; }

    public DateOnly VerifiedAgainstDocsOn { get; }

    public ImmutableArray<CatalogResourceTypeDescriptor> ResourceTypes { get; }

    public ImmutableArray<CatalogRegion> Regions { get; }

    public string Fingerprint { get; }

    public CatalogResourceTypeDescriptor? GetResourceType(string type) =>
        ResourceTypes.FirstOrDefault(resourceType =>
            string.Equals(resourceType.Type, type, StringComparison.OrdinalIgnoreCase));

    public ImmutableArray<CatalogResourceTypeDescriptor> FindResourceTypes(string? category, string? search)
    {
        var normalizedSearch = search?.Trim();
        return ResourceTypes
            .Where(resourceType => string.IsNullOrWhiteSpace(category)
                || string.Equals(resourceType.Category, category, StringComparison.OrdinalIgnoreCase))
            .Where(resourceType => string.IsNullOrWhiteSpace(normalizedSearch)
                || resourceType.Type.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || resourceType.LabelFr.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || resourceType.LabelEn.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(resourceType => resourceType.Type, StringComparer.Ordinal)
            .ToImmutableArray();
    }
}
