using ErrorOr;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Queries.ListInvitations;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
public sealed record ListInvitationsQuery(Guid OrganizationId)
    : IQuery<ErrorOr<IReadOnlyList<InvitationSummary>>>;
