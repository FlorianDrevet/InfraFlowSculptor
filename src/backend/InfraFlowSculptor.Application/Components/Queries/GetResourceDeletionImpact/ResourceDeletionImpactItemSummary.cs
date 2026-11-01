namespace InfraFlowSculptor.Application.Components.Queries.GetResourceDeletionImpact;

public sealed record ResourceDeletionImpactItemSummary(
    string Type,
    string Name,
    string Code,
    string Message,
    bool BlocksDeletion);
