using InfraFlowSculptor.Engine.Links;

namespace InfraFlowSculptor.Application.Components.Queries.GetLinkRemovalImpact;

public sealed record LinkImpactSummary(ImpactSeverity Severity, IReadOnlyList<ImpactItem> Items);
