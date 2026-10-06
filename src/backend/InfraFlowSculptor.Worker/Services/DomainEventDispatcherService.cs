using InfraFlowSculptor.Application.Common.Events;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Persistence.Common;
using InfraFlowSculptor.Worker.Jobs;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Worker.Services;

public sealed partial class DomainEventDispatcherService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<DomainEventDispatcherService> logger) : BackgroundService
{
    private const int BatchSize = 25;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await DispatchBatchAsync(stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                DispatcherFailure(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        var dispatched = 0;
        while (dispatched < BatchSize && !cancellationToken.IsCancellationRequested)
        {
            if (!await DispatchNextAsync(cancellationToken))
            {
                break;
            }

            dispatched++;
        }

        return dispatched;
    }

    private async Task<bool> DispatchNextAsync(CancellationToken cancellationToken)
    {
        OutboxMessageEntity? message = null;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var now = clock.GetUtcNow();
            message = await dbContext.OutboxMessages
                .FromSqlInterpolated($"""
                    SELECT * FROM outbox_messages
                    WHERE sent_at IS NULL
                      AND failed_at IS NULL
                      AND queue = ''
                      AND (next_attempt_at IS NULL OR next_attempt_at <= {now})
                    ORDER BY created_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                    """)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(cancellationToken);

            if (message is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            var organizationContext = scope.ServiceProvider.GetRequiredService<WorkerOrganizationContext>();
            organizationContext.Set(message.OrganizationId);
            var eventType = typeof(IDomainEvent).Assembly.GetType(message.Type)
                ?? throw new InvalidOperationException($"Domain event type '{message.Type}' was not found.");
            var handlers = scope.ServiceProvider.GetServices<IDomainEventHandler>()
                .Where(handler => handler.EventType == eventType)
                .ToArray();

            foreach (var handler in handlers)
            {
                var handlerId = handler.HandlerType;
                var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO processed_jobs (job_id, handler_type, processed_at)
                    VALUES ({message.Id}, {handlerId}, {clock.GetUtcNow()})
                    ON CONFLICT (job_id, handler_type) DO NOTHING
                    """, cancellationToken);
                if (inserted == 0)
                {
                    continue;
                }

                await handler.HandleAsync(
                    message.Payload,
                    new DomainEventExecutionContext(message.Id, message.OrganizationId, clock.GetUtcNow()),
                    cancellationToken);
            }

            message.MarkSent(clock.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (message is null)
            {
                throw;
            }

            EventFailure(logger, exception, message.Id);
            await RecordFailureAsync(message.Id, cancellationToken);
            return true;
        }
    }

    private async Task RecordFailureAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var retryScope = scopeFactory.CreateAsyncScope();
        var dbContext = retryScope.ServiceProvider.GetRequiredService<IfsDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var message = await dbContext.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT * FROM outbox_messages
                WHERE id = {eventId} AND sent_at IS NULL AND failed_at IS NULL
                FOR UPDATE
                """)
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);
        if (message is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var permanentlyFailed = message.RecordFailure(clock.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (permanentlyFailed)
        {
            EventAbandoned(logger, message.Id, message.Attempts);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The domain event dispatcher failed; it will retry shortly.")]
    private static partial void DispatcherFailure(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to dispatch domain event {EventId}; retry state will be recorded.")]
    private static partial void EventFailure(ILogger logger, Exception exception, Guid eventId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Domain event {EventId} reached its retry limit after {AttemptCount} attempts and will not be dispatched again.")]
    private static partial void EventAbandoned(ILogger logger, Guid eventId, int attemptCount);
}
