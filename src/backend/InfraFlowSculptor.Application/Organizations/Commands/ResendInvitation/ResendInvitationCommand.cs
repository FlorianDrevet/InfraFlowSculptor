using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.ResendInvitation;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
[Audited(AuditActions.InvitationResent, ObjectType.Invitation)]
public sealed record ResendInvitationCommand(Guid OrganizationId, Guid InvitationId)
    : ICommand<ErrorOr<InvitationSummary>>;
