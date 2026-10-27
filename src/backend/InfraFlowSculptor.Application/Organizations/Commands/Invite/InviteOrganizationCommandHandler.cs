using ErrorOr;
using InfraFlowSculptor.Application.Common.Notifications;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Plans;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Commands.Invite;

public sealed class InviteOrganizationCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    IPlanLimitGuard planLimitGuard,
    TimeProvider clock) : ICommandHandler<InviteOrganizationCommand, ErrorOr<InvitationSummary>>
{
    public async ValueTask<ErrorOr<InvitationSummary>> Handle(
        InviteOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId || organizationId.Value != command.OrganizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var organization = await dbContext.Query<OrganizationAggregate>()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);
        if (organization is null)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var inviter = await FindProfileAsync(currentUser.Key, cancellationToken);
        if (inviter is null || organization.Members.All(member => member.UserProfileId != inviter.Id))
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var limit = planLimitGuard.EnsureCanCreate(organizationId, LimitKind.Member);
        if (limit.IsError)
        {
            return limit.Errors;
        }

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var access = await InvitationAccessPolicy.ValidateAsync(
            dbContext,
            organization,
            normalizedEmail,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        var duplicateInvitation = await dbContext.Query<InvitationAggregate>()
            .AnyAsync(invitation => invitation.OrganizationId == organizationId
                && invitation.Email == normalizedEmail
                && invitation.Status == InvitationStatus.Pending
                && invitation.ExpiresAt > clock.GetUtcNow(), cancellationToken);
        if (duplicateInvitation)
        {
            return Error.Conflict("INVITATION_ALREADY_PENDING", "A pending invitation already exists for this address.");
        }

        var token = InvitationTokenGenerator.Generate();
        var created = InvitationAggregate.Create(
            InvitationId.New(clock),
            organizationId,
            normalizedEmail,
            command.Roles,
            token,
            inviter.PreferredLanguage,
            clock.GetUtcNow());
        if (created.IsError)
        {
            return created.Errors;
        }

        dbContext.Query<InvitationAggregate>().Add(created.Value);
        organization.RecordInvitationChange();
        return InvitationSummary.From(created.Value, clock.GetUtcNow());
    }

    private Task<UserProfile?> FindProfileAsync(UserKey userKey, CancellationToken cancellationToken) =>
        dbContext.Query<UserProfile>().SingleOrDefaultAsync(profile => profile.TenantId == userKey.TenantId
            && profile.ObjectId == userKey.ObjectId, cancellationToken);

}
