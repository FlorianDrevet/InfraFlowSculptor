using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record LinkPreviewIdentityResponse(
    Guid EnvironmentId,
    string EnvironmentCode,
    Guid ResourceId,
    string ResourceName,
    LinkIdentityKind Kind,
    Guid? IdentityResourceId,
    string? IdentityResourceName,
    bool IsReused);
