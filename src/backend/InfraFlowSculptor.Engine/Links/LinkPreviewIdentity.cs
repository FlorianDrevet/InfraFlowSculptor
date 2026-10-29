using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed record LinkPreviewIdentity(
    Guid EnvironmentId,
    string EnvironmentCode,
    Guid ResourceId,
    string ResourceName,
    LinkIdentityKind Kind,
    Guid? IdentityResourceId,
    string? IdentityResourceName,
    bool IsReused);
