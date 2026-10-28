using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Queries.ListMyOrganizations;

public sealed class ListMyOrganizationsQueryHandler(IIfsDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<ListMyOrganizationsQuery, ErrorOr<IReadOnlyList<OrganizationSummary>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<OrganizationSummary>>> Handle(
        ListMyOrganizationsQuery query,
        CancellationToken cancellationToken)
    {
        var profileId = await dbContext.Query<UserProfile>()
            .Where(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId)
            .Select(profile => profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (profileId.Value == Guid.Empty)
        {
            return Error.NotFound("USER_PROFILE_NOT_FOUND", "The authenticated user profile was not found.");
        }

        var organizations = await dbContext.Query<OrganizationAggregate>()
            .Include(organization => organization.Members)
            .Where(organization => organization.Members.Any(member => member.UserProfileId == profileId))
            .OrderBy(organization => organization.Name)
            .ToListAsync(cancellationToken);

        IReadOnlyList<OrganizationSummary> result = organizations
            .Select(organization => new OrganizationSummary(
                organization.Id.Value,
                organization.Name,
                organization.Members
                    .Where(member => member.UserProfileId == profileId)
                    .SelectMany(member => member.Roles)
                    .ToArray(),
                organization.Version,
                organization.AllowedEmailDomains,
                organization.AllowedTenantIds))
            .ToArray();

        return result.ToArray();
    }
}
