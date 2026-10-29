using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Links;

public sealed record LinkPreviewRoleAssignment(
    Guid EnvironmentId,
    string EnvironmentCode,
    Guid PrincipalResourceId,
    string PrincipalResourceName,
    string RoleName,
    LinkAccessScope Scope,
    Guid ScopeResourceId,
    string ScopeResourceName,
    Guid TargetResourceId,
    string TargetResourceName,
    bool IsReused);
