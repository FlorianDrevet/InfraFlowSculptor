using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Admin;

public sealed class ProjectPurger(IfsDbContext dbContext)
{
    public async Task<int> PurgeDeletedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var projectIds = (await dbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value"
            FROM projects
            WHERE deleted_at <= {cutoff}
            ORDER BY id
            FOR UPDATE
            """)
            .ToArrayAsync(cancellationToken))
            .Select(id => new ProjectId(id))
            .ToArray();
        if (projectIds.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var projectsToPurge = dbContext.Projects
            .IgnoreQueryFilters()
            .Where(project => projectIds.Contains(project.Id) && project.DeletedAt <= cutoff);
        await dbContext.GitConnectionPublicationUses
            .IgnoreQueryFilters()
            .Where(usage => projectIds.Contains(usage.ProjectId))
            .ExecuteDeleteAsync(cancellationToken);
        var purged = await projectsToPurge.ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return purged;
    }
}
