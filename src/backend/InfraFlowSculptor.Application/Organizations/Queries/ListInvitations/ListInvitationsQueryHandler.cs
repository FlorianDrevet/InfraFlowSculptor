using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Queries.ListInvitations;

public sealed class ListInvitationsQueryHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    TimeProvider clock) : IQueryHandler<ListInvitationsQuery, ErrorOr<IReadOnlyList<InvitationSummary>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<InvitationSummary>>> Handle(
        ListInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId || organizationId.Value != query.OrganizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var invitations = await dbContext.Query<InvitationAggregate>()
            .Where(invitation => invitation.OrganizationId == organizationId)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        return invitations.Select(invitation => InvitationSummary.From(invitation, now)).ToArray();
    }
}
