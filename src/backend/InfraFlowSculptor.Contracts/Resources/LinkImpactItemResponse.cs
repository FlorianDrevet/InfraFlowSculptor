namespace InfraFlowSculptor.Contracts.Resources;

public sealed record LinkImpactItemResponse(
    string Code,
    ResourceImpactSeverity Severity,
    string Message,
    Guid? LinkId,
    Guid? ResourceId);
