using InfraFlowSculptor.Application.Common.Jobs;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Admin;

public sealed class EfScheduledJobLeaseStore(IfsDbContext dbContext) : IScheduledJobLeaseStore
{
    public async Task<bool> TryAcquireAsync(
        string jobName,
        string holder,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO scheduled_job_leases (job_name, holder, expires_at)
            VALUES ({jobName}, {holder}, {expiresAt})
            ON CONFLICT (job_name) DO UPDATE
            SET holder = EXCLUDED.holder,
                expires_at = EXCLUDED.expires_at
            WHERE scheduled_job_leases.expires_at <= {now}
            """, cancellationToken);

        return affected == 1;
    }

    public async Task<bool> RenewAsync(
        string jobName,
        string holder,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE scheduled_job_leases
            SET expires_at = {expiresAt}
            WHERE job_name = {jobName} AND holder = {holder}
            """, cancellationToken);

        return affected == 1;
    }

    public async Task ReleaseAsync(
        string jobName,
        string holder,
        DateTimeOffset nextEligibleAt,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE scheduled_job_leases
            SET expires_at = {nextEligibleAt}
            WHERE job_name = {jobName} AND holder = {holder}
            """, cancellationToken);
    }
}
