namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ResourceDeletionImpactResponse(
    int ResourceVersion,
    ResourceDeletionImpactStatus Status,
    ResourceImpactSeverity Severity,
    IReadOnlyList<ResourceDeletionImpactArea> PendingAreas,
    IReadOnlyList<ResourceDeletionImpactItemResponse> Items);
