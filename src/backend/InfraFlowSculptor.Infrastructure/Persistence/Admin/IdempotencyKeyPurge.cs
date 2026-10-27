using Microsoft.EntityFrameworkCore;
using InfraFlowSculptor.Infrastructure.Persistence;

namespace InfraFlowSculptor.Infrastructure.Persistence.Admin;

public sealed class IdempotencyKeyPurge(IfsDbContext dbContext)
{
    public async Task<int> DeleteExpiredAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        var organizationKeysDeleted = await dbContext.IdempotencyKeys
            .IgnoreQueryFilters()
            .Where(key => key.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
        var userKeysDeleted = await dbContext.UserIdempotencyKeys
            .Where(key => key.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        return organizationKeysDeleted + userKeysDeleted;
    }
}
