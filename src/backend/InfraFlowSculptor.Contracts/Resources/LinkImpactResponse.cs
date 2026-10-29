namespace InfraFlowSculptor.Contracts.Resources;

public sealed record LinkImpactResponse(
    int ModelVersion,
    ResourceImpactSeverity Severity,
    IReadOnlyList<LinkImpactItemResponse> Items);
