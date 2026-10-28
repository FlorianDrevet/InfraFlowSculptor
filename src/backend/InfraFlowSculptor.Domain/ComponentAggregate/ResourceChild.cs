using System.Text.Json;
using ErrorOr;

namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class ResourceChild
{
    private readonly List<ResourceValueNode> values = [];

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NameKey { get; private set; } = string.Empty;

    public IReadOnlyCollection<ResourceValueNode> Values => values.AsReadOnly();

    private ResourceChild()
    {
    }

    private ResourceChild(Guid id, string type, string name)
    {
        Id = id;
        Type = type;
        Name = name;
        NameKey = name.ToUpperInvariant();
    }

    internal static ErrorOr<ResourceChild> Create(
        Guid id,
        string type,
        string name,
        IReadOnlyDictionary<string, JsonElement> properties)
    {
        var normalizedType = type?.Trim() ?? string.Empty;
        var normalizedName = name?.Trim() ?? string.Empty;
        if (id == Guid.Empty || normalizedType.Length is < 1 or > 100)
        {
            return Error.Validation("RESOURCE_CHILD_IDENTITY_INVALID", "A child identifier and descriptor type are required.");
        }

        if (normalizedName.Length is < 1 or > 90)
        {
            return Error.Validation("RESOURCE_CHILD_NAME_INVALID", "A child name must contain between 1 and 90 characters.");
        }

        var child = new ResourceChild(id, normalizedType, normalizedName);
        try
        {
            foreach (var property in properties)
            {
                child.values.AddRange(ResourceValueNode.CreateTree(property.Key, property.Value, null));
            }
        }
        catch (ArgumentException exception)
        {
            return Error.Validation("RESOURCE_CHILD_PROPERTY_VALUE_INVALID", exception.Message);
        }

        return child;
    }

    internal ErrorOr<Success> Update(string name, IReadOnlyDictionary<string, JsonElement> properties)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length is < 1 or > 90)
        {
            return Error.Validation("RESOURCE_CHILD_NAME_INVALID", "A child name must contain between 1 and 90 characters.");
        }

        var nodes = new List<ResourceValueNode>();
        try
        {
            foreach (var property in properties)
            {
                nodes.AddRange(ResourceValueNode.CreateTree(property.Key, property.Value, null));
            }
        }
        catch (ArgumentException exception)
        {
            return Error.Validation("RESOURCE_CHILD_PROPERTY_VALUE_INVALID", exception.Message);
        }

        Name = normalizedName;
        NameKey = normalizedName.ToUpperInvariant();
        values.Clear();
        values.AddRange(nodes);
        return Result.Success;
    }

    public IReadOnlyDictionary<string, JsonElement> GetProperties()
    {
        var roots = values.Where(value => value.ParentId is null);
        return roots.ToDictionary(
            root => root.PropertyName!,
            root => ResourceValueNode.ToJsonElement(values, root.Id),
            StringComparer.Ordinal);
    }
}
