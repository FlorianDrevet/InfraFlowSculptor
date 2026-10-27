using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.RevokeInvitation;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
public sealed record RevokeInvitationCommand(Guid OrganizationId, Guid InvitationId)
    : ICommand<ErrorOr<InvitationSummary>>;
