using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Organizations;

public sealed record InvitationAcceptance(
    Guid OrganizationId,
    string OrganizationName,
    string Email,
    IReadOnlyCollection<OrganizationRole> Roles);
