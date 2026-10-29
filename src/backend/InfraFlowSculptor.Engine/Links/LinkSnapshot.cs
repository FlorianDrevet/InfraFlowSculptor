using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed record LinkSnapshot(
    Guid Id,
    Guid SourceComponentId,
    Guid SourceResourceId,
    Guid TargetComponentId,
    Guid TargetResourceId,
    LinkKind Kind,
    LinkOrigin Origin,
    LinkIdentityKind IdentityKind,
    Guid? IdentityResourceId,
    IReadOnlyList<string> Roles,
    LinkAccessScope AccessScope,
    DataAccessLevel? DataAccessLevel);
