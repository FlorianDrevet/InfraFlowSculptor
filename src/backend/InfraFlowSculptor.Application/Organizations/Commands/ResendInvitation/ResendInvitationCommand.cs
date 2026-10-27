using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.ResendInvitation;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
public sealed record ResendInvitationCommand(Guid OrganizationId, Guid InvitationId)
    : ICommand<ErrorOr<InvitationSummary>>;
