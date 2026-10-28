namespace InfraFlowSculptor.Application.Components.Queries.GetResourceDeletionImpact;

public sealed record ResourceDeletionImpactSummary(
    int ResourceVersion,
    IReadOnlyList<ResourceDeletionImpactItemSummary> Items);
