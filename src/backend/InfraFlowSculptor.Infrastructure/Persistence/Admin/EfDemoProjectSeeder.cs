using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Admin;

public sealed class EfDemoProjectSeeder(IfsDbContext dbContext)
{
    public async Task SeedShopAsync(
        ProjectId projectId,
        OrganizationId organizationId,
        UserProfileId ownerId,
        UserProfileId contributorId,
        UserProfileId readerId,
        string catalogVersion,
        CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .IgnoreQueryFilters()
            .Include(candidate => candidate.RoleAssignments)
            .SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (project is null)
        {
            var ownerTenantId = await dbContext.UserProfiles
                .Where(profile => profile.Id == ownerId)
                .Select(profile => profile.TenantId)
                .SingleAsync(cancellationToken);
            project = ProjectAggregate.Create(
                projectId,
                organizationId,
                "shop",
                "shop",
                ownerTenantId,
                string.Empty,
                catalogVersion,
                ownerId).Value;
            dbContext.Projects.Add(project);
        }
        else if (project.OrganizationId != organizationId)
        {
            throw new InvalidOperationException("The demo project identifier is already used by another organization.");
        }

        AssignIfMissing(project, contributorId, ProjectRole.Contributor);
        AssignIfMissing(project, readerId, ProjectRole.Reader);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void AssignIfMissing(ProjectAggregate project, UserProfileId memberId, ProjectRole role)
    {
        if (!project.HasRole(memberId, role))
        {
            var result = project.AssignRole(memberId, role);
            if (result.IsError)
            {
                throw new InvalidOperationException($"Could not seed the demo project role: {result.FirstError.Code}.");
            }
        }
    }
}
