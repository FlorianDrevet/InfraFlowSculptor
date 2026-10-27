using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Commands.LeaveOrganization;

public sealed class LeaveOrganizationCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    IProjectOwnershipCheck projectOwnershipCheck)
    : ICommandHandler<LeaveOrganizationCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(
        LeaveOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId || organizationId.Value != command.OrganizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var profileId = await dbContext.Query<UserProfile>()
            .Where(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId)
            .Select(profile => profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var organization = await dbContext.Query<OrganizationAggregate>()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);

        if (organization is null || profileId.Value == Guid.Empty
            || organization.Members.All(member => member.UserProfileId != profileId))
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        if (await projectOwnershipCheck.WouldRemovingMemberRemoveLastProjectOwnerAsync(
                organizationId,
                profileId,
                cancellationToken))
        {
            return Error.Conflict("RG-ORG-11", "Assign another project owner before leaving the organization.");
        }

        return organization.LeaveOrganization(profileId);
    }
}
