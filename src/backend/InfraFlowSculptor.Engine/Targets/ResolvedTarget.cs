namespace InfraFlowSculptor.Engine.Targets;

public sealed record ResolvedTarget(
    Guid Id,
    string Code,
    Guid SubscriptionId,
    string Region,
    IReadOnlyDictionary<string, string> Tags,
    bool IsSingle);
