using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed partial class GitConnectionExpiryAlertScheduledJob(
    IIfsDbContext dbContext,
    GitConnectionExpiryAlertProcessor processor,
    ILogger<GitConnectionExpiryAlertScheduledJob> logger) : IScheduledJob
{
    public string Name => "git-connection-expiry-alerts";

    public TimeSpan Interval => TimeSpan.FromDays(1);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var organizationIds = await dbContext.Query<GitConnectionAggregate>()
            .IgnoreQueryFilters()
            .Where(connection => connection.AuthenticationMode == GitConnectionAuthenticationMode.PersonalAccessToken)
            .Select(connection => connection.OrganizationId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        foreach (var organizationId in organizationIds)
        {
            try
            {
                await processor.CheckOrganizationAsync(organizationId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                OrganizationCheckFailed(logger, organizationId.Value, exception.GetType().Name);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Git connection expiry alerts failed for organization {OrganizationId}: {FailureType}")]
    private static partial void OrganizationCheckFailed(ILogger logger, Guid organizationId, string failureType);
}
