using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Admin;

public sealed class EfOrganizationJobBudgetStore(IfsDbContext dbContext) : IOrganizationJobBudgetStore
{
    public async Task<bool> TryConsumeAsync(
        OrganizationId organizationId,
        int defaultJobsPerMinute,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (defaultJobsPerMinute <= 0)
        {
            return true;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO organization_budgets (organization_id, jobs_per_minute, window_started_at, jobs_processed)
            VALUES ({organizationId.Value}, {defaultJobsPerMinute}, {now}, 0)
            ON CONFLICT (organization_id) DO NOTHING
            """, cancellationToken);

        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE organization_budgets
            SET window_started_at = CASE
                    WHEN window_started_at <= {now.AddMinutes(-1)} THEN {now}
                    ELSE window_started_at
                END,
                jobs_processed = CASE
                    WHEN window_started_at <= {now.AddMinutes(-1)} THEN 1
                    ELSE jobs_processed + 1
                END
            WHERE organization_id = {organizationId.Value}
              AND (window_started_at <= {now.AddMinutes(-1)} OR jobs_processed < jobs_per_minute)
            """, cancellationToken);

        return affected == 1;
    }
}
