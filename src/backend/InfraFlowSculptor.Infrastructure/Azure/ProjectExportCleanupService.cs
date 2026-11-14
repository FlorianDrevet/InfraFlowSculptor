using InfraFlowSculptor.Application.Generation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InfraFlowSculptor.Infrastructure.Azure;

public sealed partial class ProjectExportCleanupService(
    IProjectExportStore exportStore,
    TimeProvider clock,
    ILogger<ProjectExportCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ProjectExportPolicy.CleanupInterval);
        do
        {
            try
            {
                await exportStore.DeleteExpiredAsync(clock.GetUtcNow(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogCleanupFailure(logger, exception);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Error, EventId = 1, Message = "Expired project exports could not be removed.")]
    private static partial void LogCleanupFailure(ILogger logger, Exception exception);
}
