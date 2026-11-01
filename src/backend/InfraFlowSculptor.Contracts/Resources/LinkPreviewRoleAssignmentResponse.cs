using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record LinkPreviewRoleAssignmentResponse(
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
