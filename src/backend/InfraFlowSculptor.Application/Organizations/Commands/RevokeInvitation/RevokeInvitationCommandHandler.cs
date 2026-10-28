using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Commands.RevokeInvitation;

public sealed class RevokeInvitationCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    TimeProvider clock) : ICommandHandler<RevokeInvitationCommand, ErrorOr<InvitationSummary>>
{
    public async ValueTask<ErrorOr<InvitationSummary>> Handle(
        RevokeInvitationCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId || organizationId.Value != command.OrganizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var organization = await dbContext.Query<OrganizationAggregate>()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);
        var invitation = await dbContext.Query<InvitationAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == new InvitationId(command.InvitationId)
                && candidate.OrganizationId == organizationId, cancellationToken);
        var profile = await dbContext.Query<UserProfile>().SingleOrDefaultAsync(candidate =>
            candidate.TenantId == currentUser.Key.TenantId && candidate.ObjectId == currentUser.Key.ObjectId,
            cancellationToken);
        if (organization is null
            || invitation is null
            || profile is null
            || organization.Members.All(member => member.UserProfileId != profile.Id))
        {
            return Error.NotFound("NOT_FOUND", "The organization or invitation was not found.");
        }

        var revoked = invitation.Revoke(clock.GetUtcNow());
        return revoked.IsError
            ? revoked.Errors
            : InvitationSummary.From(invitation, clock.GetUtcNow());
    }
}
