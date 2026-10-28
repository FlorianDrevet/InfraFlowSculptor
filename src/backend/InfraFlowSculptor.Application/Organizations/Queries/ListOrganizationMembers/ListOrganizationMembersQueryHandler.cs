using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Queries.ListOrganizationMembers;

public sealed class ListOrganizationMembersQueryHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization)
    : IQueryHandler<ListOrganizationMembersQuery, ErrorOr<IReadOnlyList<OrganizationMemberSummary>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<OrganizationMemberSummary>>> Handle(
        ListOrganizationMembersQuery query,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId || organizationId.Value != query.OrganizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var currentProfileId = await dbContext.Query<UserProfile>()
            .Where(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId)
            .Select(profile => profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var organization = await dbContext.Query<OrganizationAggregate>()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);

        if (organization is null
            || !organization.Members.Any(member => member.UserProfileId == currentProfileId))
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var memberProfileIds = organization.Members.Select(member => member.UserProfileId).ToArray();
        var profiles = await dbContext.Query<UserProfile>()
            .Where(profile => memberProfileIds.Contains(profile.Id))
            .ToDictionaryAsync(profile => profile.Id, cancellationToken);

        if (profiles.Count != memberProfileIds.Length)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var members = organization.Members
            .Select(member =>
            {
                var profile = profiles[member.UserProfileId];
                return new OrganizationMemberSummary(
                    member.UserProfileId.Value,
                    profile.DisplayName,
                    profile.Email,
                    member.Roles,
                    organization.IsOutsideAccessRestrictions(profile));
            })
            .OrderBy(member => member.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return members;
    }
}
