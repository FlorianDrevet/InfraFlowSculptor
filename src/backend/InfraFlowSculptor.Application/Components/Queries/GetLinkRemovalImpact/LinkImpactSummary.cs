using InfraFlowSculptor.Engine.Links;

namespace InfraFlowSculptor.Application.Components.Queries.GetLinkRemovalImpact;

public sealed record LinkImpactSummary(int ModelVersion, ImpactSeverity Severity, IReadOnlyList<ImpactItem> Items);
