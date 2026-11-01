using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed record RoleAssignmentSnapshot(
    Guid OwnerComponentId,
    RolePrincipalKind PrincipalKind,
    Guid PrincipalResourceId,
    string RoleName,
    LinkAccessScope ScopeKind,
    Guid ScopeResourceId,
    Guid TargetComponentId,
    bool IsExternal,
    string DeterministicName,
    IReadOnlyList<Guid> TargetResourceIds,
    IReadOnlyList<WiringOrigin> Origins);
