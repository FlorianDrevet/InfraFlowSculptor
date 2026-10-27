using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Contracts.Organizations;

public sealed record InvitationPreviewResponse(
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
