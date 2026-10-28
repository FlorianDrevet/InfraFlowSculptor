using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Organizations;

public sealed record InvitationPreview(
    Guid OrganizationId,
    string OrganizationName,
    string Email,
    IReadOnlyCollection<OrganizationRole> Roles,
    InvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool EmailVerified,
    bool EmailMatches,
    bool TenantAllowed);
