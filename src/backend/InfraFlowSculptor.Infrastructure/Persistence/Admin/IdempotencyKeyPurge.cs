using Microsoft.EntityFrameworkCore;
using InfraFlowSculptor.Infrastructure.Persistence;

namespace InfraFlowSculptor.Infrastructure.Persistence.Admin;

public sealed class IdempotencyKeyPurge(IfsDbContext dbContext)
{
    public Task<int> DeleteExpiredAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default) =>
        dbContext.IdempotencyKeys
            .IgnoreQueryFilters()
            .Where(key => key.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
}
