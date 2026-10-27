using ErrorOr;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Queries.GetInvitationPreview;

public sealed record GetInvitationPreviewQuery(string Token) : IQuery<ErrorOr<InvitationPreview>>;
