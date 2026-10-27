using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Authentication;

public sealed class EfProjectOwnershipCheck(IIfsDbContext dbContext) : IProjectOwnershipCheck
{
    public async Task<bool> WouldRemovingMemberRemoveLastProjectOwnerAsync(
        OrganizationId organizationId,
        UserProfileId userProfileId,
        CancellationToken cancellationToken)
    {
        var ownerCounts = await dbContext.Query<ProjectAggregate>()
            .AsNoTracking()
            .Where(project => project.OrganizationId == organizationId)
            .Where(project => project.RoleAssignments.Any(assignment =>
                assignment.Principal == userProfileId && assignment.Role == ProjectRole.Owner))
            .Select(project => project.RoleAssignments.Count(assignment => assignment.Role == ProjectRole.Owner))
            .ToArrayAsync(cancellationToken);

        return ownerCounts.Any(ownerCount => ownerCount == 1);
    }
}
