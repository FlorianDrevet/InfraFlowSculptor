using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Application.Organizations;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.AcceptInvitation;

[Audited(AuditActions.InvitationAccepted, ObjectType.Invitation)]
public sealed record AcceptInvitationCommand(string Token) : ICommand<ErrorOr<InvitationAcceptance>>;
