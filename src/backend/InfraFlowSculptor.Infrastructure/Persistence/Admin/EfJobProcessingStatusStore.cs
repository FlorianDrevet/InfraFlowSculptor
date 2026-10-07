using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Admin;

public sealed class EfJobProcessingStatusStore(IfsDbContext dbContext) : IJobProcessingStatusStore
{
    public async Task<int> GetProcessedCountAsync(
        Guid jobId,
        OrganizationId organizationId,
        CancellationToken cancellationToken = default)
    {
        var wasRelayed = await dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .AnyAsync(message => message.Id == jobId
                && message.OrganizationId == organizationId
                && message.SentAt != null, cancellationToken);
        if (!wasRelayed)
        {
            return 0;
        }

        return await dbContext.ProcessedJobs
            .CountAsync(job => job.JobId == jobId, cancellationToken);
    }
}
