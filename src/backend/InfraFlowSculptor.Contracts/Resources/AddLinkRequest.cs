using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record AddLinkRequest(
    Guid TargetComponentId,
    Guid TargetResourceId,
    LinkKind Kind,
    LinkIdentityKind IdentityKind,
    Guid? IdentityResourceId,
    IReadOnlyList<string> Roles,
    LinkAccessScope AccessScope,
    DataAccessLevel? DataAccessLevel,
    int ExpectedComponentVersion);
