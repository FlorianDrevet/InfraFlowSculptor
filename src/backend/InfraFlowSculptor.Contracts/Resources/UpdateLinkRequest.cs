using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record UpdateLinkRequest(
    Guid TargetComponentId,
    Guid TargetResourceId,
    LinkKind Kind,
    LinkIdentityKind IdentityKind,
    Guid? IdentityResourceId,
    IReadOnlyList<string> Roles,
    LinkAccessScope AccessScope,
    DataAccessLevel? DataAccessLevel,
    int ExpectedLinkVersion);
