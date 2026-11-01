using System.Text.Json;

namespace InfraFlowSculptor.Engine.Model;

public sealed record ResourceSnapshot(
    Guid Id,
    Guid ResourceGroupId,
    string Type,
    string LogicalName,
    string Description,
    bool IsExisting,
    IReadOnlyDictionary<string, JsonElement> Properties,
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, JsonElement>> Overrides,
    IReadOnlyDictionary<Guid, bool> Presences,
    IReadOnlyDictionary<Guid, string> ExistingIds,
    IReadOnlyDictionary<Guid, string> ForcedNames,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<ResourceChildSnapshot> Children)
{
    public int Version { get; init; } = 1;

    public Guid? DefaultIdentityResourceId { get; init; }
}
