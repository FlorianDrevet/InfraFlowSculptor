using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Commands.RemoveProjectRole;

[RequiresPermission(Permission.MembersManage)]
[Audited(AuditActions.ProjectRoleRemoved, ObjectType.ProjectRoleAssignment)]
public sealed record RemoveProjectRoleCommand(
    Guid ProjectId,
    Guid MemberId,
    ProjectRole Role,
    ProjectRoleScope Scope,
    int ExpectedVersion)
    : ICommand<ErrorOr<ProjectRoleAssignmentSummary>>, IProjectScopedRequest;
