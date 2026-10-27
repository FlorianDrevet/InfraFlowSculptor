using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Admin;

public sealed class ProjectPurger(IfsDbContext dbContext)
{
    public Task<int> PurgeDeletedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
        dbContext.Projects
            .IgnoreQueryFilters()
            .Where(project => project.DeletedAt <= cutoff)
            .ExecuteDeleteAsync(cancellationToken);
}
