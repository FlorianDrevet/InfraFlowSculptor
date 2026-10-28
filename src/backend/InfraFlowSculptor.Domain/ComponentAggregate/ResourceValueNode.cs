using System.Text.Json;

namespace InfraFlowSculptor.Domain.ComponentAggregate;

public sealed class ResourceValueNode
{
    private const int MaximumDepth = 32;
    private const int MaximumNodes = 4096;

    public Guid Id { get; private set; }

    public Guid? TargetId { get; private set; }

    public Guid? ParentId { get; private set; }

    public string? PropertyName { get; private set; }

    public string? MemberName { get; private set; }

    public int? Position { get; private set; }

    public ResourceValueKind Kind { get; private set; }

    public string? StringValue { get; private set; }

    public bool? BooleanValue { get; private set; }

    public long? IntegerValue { get; private set; }

    public decimal? NumberValue { get; private set; }

    private ResourceValueNode()
    {
    }

    private ResourceValueNode(
        Guid id,
        Guid? targetId,
        Guid? parentId,
        string? propertyName,
        string? memberName,
        int? position,
        ResourceValueKind kind,
        string? stringValue,
        bool? booleanValue,
        long? integerValue,
        decimal? numberValue)
    {
        Id = id;
        TargetId = targetId;
        ParentId = parentId;
        PropertyName = propertyName;
        MemberName = memberName;
        Position = position;
        Kind = kind;
        StringValue = stringValue;
        BooleanValue = booleanValue;
        IntegerValue = integerValue;
        NumberValue = numberValue;
    }

    internal static IReadOnlyList<ResourceValueNode> CreateTree(
        string propertyName,
        JsonElement value,
        Guid? targetId)
    {
        var nodes = new List<ResourceValueNode>();
        Add(value, null, propertyName, null, null, targetId, nodes, 0);
        return nodes;
    }

    internal static JsonElement ToJsonElement(IReadOnlyCollection<ResourceValueNode> nodes, Guid rootId)
    {
        var byParent = nodes.Where(node => node.ParentId is not null)
            .GroupBy(node => node.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var byId = nodes.ToDictionary(node => node.Id);
        return JsonSerializer.SerializeToElement(Build(rootId, byId, byParent));
    }

    private static object? Build(
        Guid nodeId,
        IReadOnlyDictionary<Guid, ResourceValueNode> byId,
        IReadOnlyDictionary<Guid, ResourceValueNode[]> byParent)
    {
        var node = byId[nodeId];
        return node.Kind switch
        {
            ResourceValueKind.Absent => null,
            ResourceValueKind.Textual => node.StringValue,
            ResourceValueKind.Truth => node.BooleanValue,
            ResourceValueKind.WholeNumber => node.IntegerValue,
            ResourceValueKind.DecimalNumber => node.NumberValue,
            ResourceValueKind.Sequence => Children(byParent, node.Id)
                .OrderBy(child => child.Position)
                .Select(child => Build(child.Id, byId, byParent))
                .ToArray(),
            ResourceValueKind.Map => Children(byParent, node.Id)
                .OrderBy(child => child.MemberName, StringComparer.Ordinal)
                .ToDictionary(child => child.MemberName!, child => Build(child.Id, byId, byParent), StringComparer.Ordinal),
            _ => throw new InvalidOperationException($"Unsupported resource value kind: {node.Kind}.")
        };
    }

    private static ResourceValueNode[] Children(
        IReadOnlyDictionary<Guid, ResourceValueNode[]> byParent,
        Guid parentId) => byParent.TryGetValue(parentId, out var children) ? children : [];

    private static void Add(
        JsonElement value,
        Guid? parentId,
        string? propertyName,
        string? memberName,
        int? position,
        Guid? targetId,
        List<ResourceValueNode> nodes,
        int depth)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(depth, MaximumDepth, nameof(value));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(nodes.Count, MaximumNodes, nameof(value));
        if (parentId is null && string.IsNullOrWhiteSpace(propertyName) || propertyName?.Length > 256
            || memberName?.Length > 256 || position < 0)
        {
            throw new ArgumentException("Resource property names and array positions must fit the relational value schema.", nameof(value));
        }

        var id = Guid.NewGuid();
        var kind = ResourceValueKind.Absent;
        string? stringValue = null;
        bool? booleanValue = null;
        long? integerValue = null;
        decimal? numberValue = null;
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
                kind = ResourceValueKind.Absent;
                break;
            case JsonValueKind.String:
                kind = ResourceValueKind.Textual;
                stringValue = value.GetString();
                break;
            case JsonValueKind.True:
                kind = ResourceValueKind.Truth;
                booleanValue = true;
                break;
            case JsonValueKind.False:
                kind = ResourceValueKind.Truth;
                booleanValue = false;
                break;
            case JsonValueKind.Number when value.TryGetInt64(out var integer):
                kind = ResourceValueKind.WholeNumber;
                integerValue = integer;
                break;
            case JsonValueKind.Number when value.TryGetDecimal(out var number):
                kind = ResourceValueKind.DecimalNumber;
                numberValue = number;
                break;
            case JsonValueKind.Number:
                throw new ArgumentOutOfRangeException(nameof(value), "A numeric value must fit in a decimal.");
            case JsonValueKind.Array:
                kind = ResourceValueKind.Sequence;
                break;
            case JsonValueKind.Object:
                kind = ResourceValueKind.Map;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), "Undefined JSON values cannot be stored.");
        }

        nodes.Add(new ResourceValueNode(
            id,
            parentId is null ? targetId : null,
            parentId,
            parentId is null ? propertyName : null,
            memberName,
            position,
            kind,
            stringValue,
            booleanValue,
            integerValue,
            numberValue));

        if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in value.EnumerateArray())
            {
                Add(child, id, null, null, index++, null, nodes, depth + 1);
            }
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var child in value.EnumerateObject())
            {
                if (!names.Add(child.Name))
                {
                    throw new ArgumentException("Object member names must be unique.", nameof(value));
                }

                Add(child.Value, id, null, child.Name, null, null, nodes, depth + 1);
            }
        }
    }
}
