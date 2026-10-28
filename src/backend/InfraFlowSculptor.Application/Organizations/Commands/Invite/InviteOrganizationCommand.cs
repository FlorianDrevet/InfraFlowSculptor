using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.Invite;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
[Audited(AuditActions.InvitationSent, ObjectType.Invitation)]
public sealed record InviteOrganizationCommand(
    Guid OrganizationId,
    string Email,
    IReadOnlyCollection<OrganizationRole> Roles) : ICommand<ErrorOr<InvitationSummary>>;
