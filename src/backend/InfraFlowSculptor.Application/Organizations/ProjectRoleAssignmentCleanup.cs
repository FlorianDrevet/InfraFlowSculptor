using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations;

internal static class ProjectRoleAssignmentCleanup
{
    public static async Task<ErrorOr<Success>> RemoveForMemberAsync(
        IIfsDbContext dbContext,
        OrganizationId organizationId,
        UserProfileId profileId,
        CancellationToken cancellationToken)
    {
        var projects = await dbContext.Query<ProjectAggregate>()
            .Include(project => project.RoleAssignments)
            .Where(project => project.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        foreach (var project in projects)
        {
            var removed = project.RemoveAllRolesForMember(profileId);
            if (removed.IsError)
            {
                return removed.Errors;
            }
        }

        return Result.Success;
    }
}
