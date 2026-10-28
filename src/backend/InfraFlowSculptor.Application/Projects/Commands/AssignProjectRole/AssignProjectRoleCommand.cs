using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Commands.AssignProjectRole;

[RequiresPermission(Permission.MembersManage)]
[Audited(AuditActions.ProjectRoleAssigned, ObjectType.ProjectRoleAssignment)]
public sealed record AssignProjectRoleCommand(
    Guid ProjectId,
    Guid MemberId,
    ProjectRole Role,
    ProjectRoleScope Scope,
    int ExpectedVersion)
    : ICommand<ErrorOr<ProjectRoleAssignmentSummary>>, IProjectScopedRequest;
