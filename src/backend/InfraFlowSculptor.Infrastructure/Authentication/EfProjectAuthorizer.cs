using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Authentication;

public sealed class EfProjectAuthorizer(
    IIfsDbContext dbContext,
    IOrganizationRoleAuthorizer organizationRoleAuthorizer)
    : IProjectAuthorizer
{
    public async Task<ProjectAuthorizationContext?> GetProjectAuthorizationContextAsync(
        UserKey userKey,
        OrganizationId organizationId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var profileId = await FindProfileIdAsync(userKey, cancellationToken);
        if (profileId.Value == Guid.Empty)
        {
            return null;
        }

        var project = await dbContext.Query<ProjectAggregate>()
            .AsNoTracking()
            .Include(candidate => candidate.RoleAssignments)
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == projectId && candidate.OrganizationId == organizationId,
                cancellationToken);
        if (project is null)
        {
            return null;
        }

        var permissions = project.EffectivePermissions(profileId);
        if (permissions.Count > 0)
        {
            return new ProjectAuthorizationContext(permissions, project.HasRole(profileId, ProjectRole.Owner));
        }

        var isOrganizationAdministrator = await organizationRoleAuthorizer.HasRoleAsync(
            userKey,
            organizationId,
            OrganizationRole.Administrator,
            cancellationToken);
        return isOrganizationAdministrator
            ? new ProjectAuthorizationContext(
                new HashSet<string>([Permission.ProjectRead], StringComparer.Ordinal),
                IsOwner: false)
            : null;
    }

    public async Task<bool> CanAssignRoleAsync(
        UserKey userKey,
        OrganizationId organizationId,
        ProjectId projectId,
        ProjectRole role,
        CancellationToken cancellationToken)
    {
        var authorization = await GetProjectAuthorizationContextAsync(
            userKey,
            organizationId,
            projectId,
            cancellationToken);

        return authorization is not null
            && ProjectRoles.CanAssign(authorization.Permissions, authorization.IsOwner, role);
    }

    private async Task<UserProfileId> FindProfileIdAsync(UserKey userKey, CancellationToken cancellationToken) =>
        await dbContext.Query<UserProfile>()
            .AsNoTracking()
            .Where(profile => profile.TenantId == userKey.TenantId && profile.ObjectId == userKey.ObjectId)
            .Select(profile => profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
}
