using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Contracts.Organizations;

public sealed record InvitationAcceptanceResponse(
    Guid OrganizationId,
    string OrganizationName,
    string Email,
    IReadOnlyCollection<OrganizationRole> Roles);
