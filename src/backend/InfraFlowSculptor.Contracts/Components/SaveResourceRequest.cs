using System.Text.Json;

namespace InfraFlowSculptor.Contracts.Components;

public sealed record SaveResourceRequest(
    Guid ResourceGroupId,
    string Type,
    string LogicalName,
    string Description,
    bool IsExisting,
    JsonElement Properties,
    IReadOnlyDictionary<string, string> Tags,
    int ExpectedVersion)
{
    public Guid? DefaultIdentityResourceId { get; init; }
}
