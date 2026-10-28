namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ResourceDeletionImpactResponse(
    int ResourceVersion,
    ResourceDeletionImpactStatus Status,
    IReadOnlyList<ResourceDeletionImpactArea> PendingAreas,
    IReadOnlyList<ResourceDeletionImpactItemResponse> Items);
