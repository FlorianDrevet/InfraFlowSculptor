using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.ChangeMemberRoles;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
[Audited(AuditActions.OrganizationMemberRolesChanged, ObjectType.OrganizationMember)]
public sealed record ChangeMemberRolesCommand(
    Guid OrganizationId,
    Guid MemberId,
    IReadOnlyCollection<OrganizationRole> Roles)
    : ICommand<ErrorOr<OrganizationMemberSummary>>;
