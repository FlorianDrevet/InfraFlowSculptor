using ErrorOr;
using InfraFlowSculptor.Application.Organizations;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.AcceptInvitation;

public sealed record AcceptInvitationCommand(string Token) : ICommand<ErrorOr<InvitationAcceptance>>;
