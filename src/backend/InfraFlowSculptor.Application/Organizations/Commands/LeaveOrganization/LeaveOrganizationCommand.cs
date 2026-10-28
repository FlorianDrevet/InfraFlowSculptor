using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Domain.Common;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.LeaveOrganization;

[Audited(AuditActions.OrganizationMemberLeft, ObjectType.OrganizationMember)]
[Audited(AuditActions.ProjectRoleRemoved, ObjectType.ProjectRoleAssignment)]
public sealed record LeaveOrganizationCommand(Guid OrganizationId) : ICommand<ErrorOr<Success>>;
