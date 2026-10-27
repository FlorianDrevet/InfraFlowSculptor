using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.Invite;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
public sealed record InviteOrganizationCommand(
    Guid OrganizationId,
    string Email,
    IReadOnlyCollection<OrganizationRole> Roles) : ICommand<ErrorOr<InvitationSummary>>;
