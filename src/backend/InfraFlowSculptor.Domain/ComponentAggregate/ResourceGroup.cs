using System.Text.RegularExpressions;
using ErrorOr;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.ComponentAggregate;

[AuditedObject(ObjectType.ResourceGroup, nameof(ResourceGroup.Id), nameof(ResourceGroup.OrganizationId))]
public sealed partial class ResourceGroup : Entity<Guid>, IHasVersion
{
    private static readonly HashSet<string> ReservedTagKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ifs-project", "ifs-component", "ifs-environment", "managed-by"
    };

    private readonly List<ResourceGroupTag> tags = [];
    private readonly List<ResourceGroupNameOverride> forcedNames = [];

    public ProjectId ProjectId { get; private set; }

    public OrganizationId OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Region { get; private set; }

    public IReadOnlyCollection<ResourceGroupTag> Tags => tags.AsReadOnly();

    public IReadOnlyCollection<ResourceGroupNameOverride> ForcedNames => forcedNames.AsReadOnly();

    public int Version { get; private set; } = 1;

    private ResourceGroup()
    {
    }

    private ResourceGroup(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        string name,
        string? region,
        IReadOnlyCollection<KeyValuePair<string, string>> tags,
        IReadOnlyCollection<KeyValuePair<Guid, string>> forcedNames) : base(id)
    {
        ProjectId = projectId;
        OrganizationId = organizationId;
        Name = name;
        Region = region;
        ReplaceTags(tags);
        ReplaceForcedNames(forcedNames);
    }

    internal static ErrorOr<ResourceGroup> Create(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        string name,
        string? region,
        IEnumerable<KeyValuePair<string, string>>? tags = null,
        IEnumerable<KeyValuePair<Guid, string>>? forcedNames = null)
    {
        if (id == Guid.Empty)
        {
            return Error.Validation("RESOURCE_GROUP_ID_REQUIRED", "A resource group identifier is required.");
        }

        var normalized = Validate(name, region, tags, forcedNames);
        if (normalized.IsError)
        {
            return normalized.Errors;
        }

        return new ResourceGroup(
            id,
            projectId,
            organizationId,
            normalized.Value.Name,
            normalized.Value.Region,
            normalized.Value.Tags,
            normalized.Value.ForcedNames);
    }

    internal ErrorOr<Success> Update(
        string name,
        string? region,
        IEnumerable<KeyValuePair<string, string>>? tags = null,
        IEnumerable<KeyValuePair<Guid, string>>? forcedNames = null)
    {
        var normalized = Validate(name, region, tags, forcedNames);
        if (normalized.IsError)
        {
            return normalized.Errors;
        }

        Name = normalized.Value.Name;
        Region = normalized.Value.Region;
        ReplaceTags(normalized.Value.Tags);
        ReplaceForcedNames(normalized.Value.ForcedNames);
        Version = checked(Version + 1);
        return Result.Success;
    }

    private static ErrorOr<ResourceGroupValues> Validate(
        string name,
        string? region,
        IEnumerable<KeyValuePair<string, string>>? tags,
        IEnumerable<KeyValuePair<Guid, string>>? forcedNames)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedRegion = string.IsNullOrWhiteSpace(region) ? null : region.Trim();
        if (!NamePattern().IsMatch(normalizedName))
        {
            return Error.Validation("RESOURCE_GROUP_NAME_INVALID", "The logical name must match ^[a-z][a-z0-9-]{0,39}$.");
        }

        if (normalizedRegion is { Length: > 64 })
        {
            return Error.Validation("RESOURCE_GROUP_REGION_INVALID", "A fixed region cannot exceed 64 characters.");
        }

        var normalizedTags = (tags ?? [])
            .Select(tag => new KeyValuePair<string, string>(tag.Key.Trim(), tag.Value.Trim()))
            .ToArray();
        if (normalizedTags.Length > 50)
        {
            return Error.Validation("RESOURCE_GROUP_TAG_LIMIT", "A maximum of 50 tags may be declared.");
        }

        if (normalizedTags.Any(tag => tag.Key.Length is < 1 or > 512
            || tag.Key.IndexOfAny(['<', '>', '%', '&', '\\', '?', '/']) >= 0
            || tag.Value.Length > 256))
        {
            return Error.Validation("RESOURCE_GROUP_TAG_FORMAT", "Tag keys must be 1-512 valid characters and values cannot exceed 256 characters.");
        }

        if (normalizedTags.Any(tag => ReservedTagKeys.Contains(tag.Key)))
        {
            return Error.Validation("RG-PRJ-06-SYSTEM-TAG-RESERVED", "System tag keys cannot be declared by a user.");
        }

        if (normalizedTags.Select(tag => tag.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedTags.Length)
        {
            return Error.Validation("RESOURCE_GROUP_TAG_DUPLICATE", "Tag keys must be unique without regard to case.");
        }

        var normalizedForcedNames = (forcedNames ?? [])
            .Select(item => new KeyValuePair<Guid, string>(item.Key, item.Value.Trim()))
            .ToArray();
        if (normalizedForcedNames.Any(item => item.Key == Guid.Empty || item.Value.Length is < 1 or > 90)
            || normalizedForcedNames.Select(item => item.Key).Distinct().Count() != normalizedForcedNames.Length)
        {
            return Error.Validation("RESOURCE_GROUP_FORCED_NAME_INVALID", "Each forced name must have an environment and contain between 1 and 90 characters.");
        }

        return new ResourceGroupValues(normalizedName, normalizedRegion, normalizedTags, normalizedForcedNames);
    }

    private void ReplaceTags(IEnumerable<KeyValuePair<string, string>> values)
    {
        tags.Clear();
        tags.AddRange(values.Select(tag => new ResourceGroupTag(tag.Key, tag.Value)));
    }

    private void ReplaceForcedNames(IEnumerable<KeyValuePair<Guid, string>> values)
    {
        forcedNames.Clear();
        forcedNames.AddRange(values.Select(item => new ResourceGroupNameOverride(item.Key, item.Value)));
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    private sealed record ResourceGroupValues(
        string Name,
        string? Region,
        IReadOnlyCollection<KeyValuePair<string, string>> Tags,
        IReadOnlyCollection<KeyValuePair<Guid, string>> ForcedNames);
}
