namespace InfraFlowSculptor.Contracts.Components;

public sealed record PreviewResourceNameRequest(
    Guid ResourceGroupId,
    string Type,
    string LogicalName,
    Guid? ResourceId = null);
