using System.Text.Json;

namespace InfraFlowSculptor.Contracts.Components;

public sealed record ResourceResponse(
    Guid Id,
    int Version,
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
    IReadOnlyList<ResourceChildResponse> Children);
