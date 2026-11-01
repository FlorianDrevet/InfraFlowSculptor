using InfraFlowSculptor.Engine.Links;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceDeletionImpact;

public sealed record ResourceDeletionImpactSummary(
    int ResourceVersion,
    IReadOnlyList<ResourceDeletionImpactItemSummary> Items,
    ImpactResult LinkImpact,
    bool HasBlockingApplicationSettings);
