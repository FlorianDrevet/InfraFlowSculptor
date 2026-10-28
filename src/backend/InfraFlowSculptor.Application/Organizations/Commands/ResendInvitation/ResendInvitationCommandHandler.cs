using ErrorOr;
using InfraFlowSculptor.Application.Common.Notifications;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Commands.ResendInvitation;

public sealed class ResendInvitationCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    TimeProvider clock) : ICommandHandler<ResendInvitationCommand, ErrorOr<InvitationSummary>>
{
    public async ValueTask<ErrorOr<InvitationSummary>> Handle(
        ResendInvitationCommand command,
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
        var inviter = await dbContext.Query<UserProfile>().SingleOrDefaultAsync(profile =>
            profile.TenantId == currentUser.Key.TenantId && profile.ObjectId == currentUser.Key.ObjectId,
            cancellationToken);
        if (organization is null
            || invitation is null
            || inviter is null
            || organization.Members.All(member => member.UserProfileId != inviter.Id))
        {
            return Error.NotFound("NOT_FOUND", "The organization or invitation was not found.");
        }

        var access = await InvitationAccessPolicy.ValidateAsync(
            dbContext,
            organization,
            invitation.Email,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        var resent = invitation.Resend(
            InvitationTokenGenerator.Generate(),
            inviter.PreferredLanguage,
            clock.GetUtcNow());
        if (resent.IsError)
        {
            return resent.Errors;
        }

        return InvitationSummary.From(invitation, clock.GetUtcNow());
    }
}
