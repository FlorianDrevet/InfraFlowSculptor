using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Authentication;

public sealed class EfOrganizationRoleAuthorizer(IIfsDbContext dbContext) : IOrganizationRoleAuthorizer
{
    public async Task<bool> HasRoleAsync(
        UserKey userKey,
        OrganizationId organizationId,
        OrganizationRole requiredRole,
        CancellationToken cancellationToken)
    {
        var profileId = await dbContext.Query<UserProfile>()
            .Where(profile => profile.TenantId == userKey.TenantId && profile.ObjectId == userKey.ObjectId)
            .Select(profile => profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return profileId.Value != Guid.Empty
            && await dbContext.Query<OrganizationAggregate>()
                .Where(organization => organization.Id == organizationId)
                .SelectMany(organization => organization.Members)
                .AnyAsync(
                    member => member.UserProfileId == profileId
                        && member.RoleValues.Contains((int)requiredRole),
                    cancellationToken);
    }
}
