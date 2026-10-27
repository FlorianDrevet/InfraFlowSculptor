using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Queries.GetInvitationPreview;

public sealed class GetInvitationPreviewQueryHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider clock) : IQueryHandler<GetInvitationPreviewQuery, ErrorOr<InvitationPreview>>
{
    public async ValueTask<ErrorOr<InvitationPreview>> Handle(
        GetInvitationPreviewQuery query,
        CancellationToken cancellationToken)
    {
        var tokenHash = InvitationAggregate.ComputeTokenHash(query.Token);
        var invitation = await dbContext.Query<InvitationAggregate>()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);
        if (invitation is null)
        {
            return Error.NotFound("NOT_FOUND", "The invitation was not found.");
        }

        var organization = await dbContext.Query<OrganizationAggregate>()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.Id == invitation.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return Error.NotFound("NOT_FOUND", "The invitation was not found.");
        }

        var verifiedEmail = currentUser.VerifiedEmail?.Value;
        var now = clock.GetUtcNow();
        return new InvitationPreview(
            organization.Id.Value,
            organization.Name,
            invitation.Email,
            invitation.Roles,
            invitation.StatusAt(now),
            invitation.CreatedAt,
            invitation.ExpiresAt,
            verifiedEmail is not null,
            verifiedEmail is not null && invitation.Email.Equals(verifiedEmail, StringComparison.OrdinalIgnoreCase),
            organization.IsInvitationTenantAllowed(currentUser.Key.TenantId));
    }
}
