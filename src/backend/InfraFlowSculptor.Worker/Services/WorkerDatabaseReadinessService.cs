using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Worker.Services;

public sealed partial class WorkerDatabaseReadinessService(
    IServiceScopeFactory scopeFactory,
    ILogger<WorkerDatabaseReadinessService> logger) : IHostedService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private bool waitingWasLogged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
                if (!pendingMigrations.Any())
                {
                    return;
                }

                if (!waitingWasLogged)
                {
                    LogWaiting(logger);
                    waitingWasLogged = true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (!waitingWasLogged)
                {
                    LogDatabaseUnavailable(logger, exception);
                    waitingWasLogged = true;
                }
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for database migrations before starting job processors.")]
    private static partial void LogWaiting(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not check database migrations; the worker will retry readiness shortly.")]
    private static partial void LogDatabaseUnavailable(ILogger logger, Exception exception);
}
