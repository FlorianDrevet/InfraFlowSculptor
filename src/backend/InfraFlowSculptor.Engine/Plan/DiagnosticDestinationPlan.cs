namespace InfraFlowSculptor.Engine.Plan;

public sealed record DiagnosticDestinationPlan(
    string ComponentCode,
    string TargetCode,
    string ResourceType,
    string LogicalName,
    string? Name,
    string? ResourceGroupName,
    Guid SubscriptionId,
    string? ResourceId,
    bool IsExisting,
    bool IsExternal);
