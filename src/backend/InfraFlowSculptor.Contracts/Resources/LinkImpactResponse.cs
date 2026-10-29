namespace InfraFlowSculptor.Contracts.Resources;

public sealed record LinkImpactResponse(ResourceImpactSeverity Severity, IReadOnlyList<LinkImpactItemResponse> Items);
