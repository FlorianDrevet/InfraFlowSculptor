namespace InfraFlowSculptor.Engine.Plan;

public sealed record ExternalReference(
    string ComponentCode,
    string TargetCode,
    string ResourceType,
    string LogicalName,
    string? Name,
    string? ResourceGroup,
    Guid SubscriptionId,
    string? ResourceId,
    bool IsExisting);
