namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ResourceDeletionImpactItemResponse(
    ResourceDeletionImpactKind Kind,
    string Type,
    string Name,
    string Code,
    ResourceImpactSeverity Severity,
    string Message,
    Guid? LinkId,
    Guid? ResourceId);
