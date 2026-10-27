using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.Invitations;

public sealed record InvitationCreated(
    Guid InvitationId,
    Guid OrganizationId,
    string Email,
    string Token,
    DateTimeOffset ExpiresAt,
    string Language) : IDomainEvent;
