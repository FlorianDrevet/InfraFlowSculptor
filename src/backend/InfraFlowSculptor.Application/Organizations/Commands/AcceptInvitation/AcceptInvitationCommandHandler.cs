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

namespace InfraFlowSculptor.Application.Organizations.Commands.AcceptInvitation;

public sealed class AcceptInvitationCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<AcceptInvitationCommand, ErrorOr<InvitationAcceptance>>
{
    public async ValueTask<ErrorOr<InvitationAcceptance>> Handle(
        AcceptInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var tokenHash = InvitationAggregate.ComputeTokenHash(command.Token);
        var invitation = await dbContext.Query<InvitationAggregate>()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);
        if (invitation is null)
        {
            return Error.NotFound("NOT_FOUND", "The invitation was not found.");
        }

        var organization = await dbContext.Query<OrganizationAggregate>()
            .IgnoreQueryFilters()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == invitation.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return Error.NotFound("NOT_FOUND", "The invitation was not found.");
        }

        if (!organization.IsInvitationTenantAllowed(currentUser.Key.TenantId))
        {
            return Error.Forbidden("RG-ORG-06", "The organization does not accept invitations from this tenant.");
        }

        var profile = await dbContext.Query<UserProfile>().SingleOrDefaultAsync(candidate =>
            candidate.TenantId == currentUser.Key.TenantId && candidate.ObjectId == currentUser.Key.ObjectId,
            cancellationToken);
        if (profile is null)
        {
            return Error.NotFound("USER_PROFILE_NOT_FOUND", "The authenticated user profile was not found.");
        }

        var now = clock.GetUtcNow();
        var wasAccepted = invitation.StatusAt(now) == InvitationStatus.Accepted;
        var accepted = invitation.Accept(currentUser.VerifiedEmail?.Value, now);
        if (accepted.IsError)
        {
            return accepted.Errors;
        }

        if (wasAccepted)
        {
            if (!organization.Members.Any(member => member.UserProfileId == profile.Id))
            {
                return Error.Conflict("INVITATION_ALREADY_ACCEPTED", "This invitation has already been accepted.");
            }

            return new InvitationAcceptance(
                organization.Id.Value,
                organization.Name,
                invitation.Email,
                invitation.Roles);
        }

        var added = organization.AddMember(profile.Id, invitation.Roles);
        if (added.IsError)
        {
            return added.Errors;
        }

        return new InvitationAcceptance(
            organization.Id.Value,
            organization.Name,
            invitation.Email,
            invitation.Roles);
    }
}
