using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Organizations;

public sealed record InvitationSummary(
    Guid Id,
    string Email,
    IReadOnlyCollection<OrganizationRole> Roles,
    InvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
{
    public static InvitationSummary From(InvitationAggregate invitation, DateTimeOffset now) =>
        new(
            invitation.Id.Value,
            invitation.Email,
            invitation.Roles,
            invitation.StatusAt(now),
            invitation.CreatedAt,
            invitation.ExpiresAt);
}
