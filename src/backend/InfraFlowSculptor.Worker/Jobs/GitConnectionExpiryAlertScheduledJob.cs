using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed class GitConnectionExpiryAlertScheduledJob(
    IIfsDbContext dbContext,
    GitConnectionExpiryAlertProcessor processor) : IScheduledJob
{
    public string Name => "git-connection-expiry-alerts";

    public TimeSpan Interval => TimeSpan.FromDays(1);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var organizationIds = await dbContext.Query<GitConnectionAggregate>()
            .IgnoreQueryFilters()
            .Where(connection => connection.UsesFallbackToken)
            .Select(connection => connection.OrganizationId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        foreach (var organizationId in organizationIds)
        {
            await processor.CheckOrganizationAsync(organizationId, cancellationToken);
        }
    }
}
