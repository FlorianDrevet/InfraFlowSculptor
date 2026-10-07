using Azure.Messaging.ServiceBus;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Worker.Services;

public sealed partial class OutboxRelayService(
    IServiceScopeFactory scopeFactory,
    ServiceBusClient serviceBusClient,
    TimeProvider clock,
    ILogger<OutboxRelayService> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan BatchSendTimeout = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var sentCount = await RelayBatchAsync(stoppingToken);
                if (sentCount == 0)
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
                RelayFailure(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task<int> RelayBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var messages = await dbContext.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT * FROM outbox_messages
                WHERE sent_at IS NULL
                  AND failed_at IS NULL
                  AND queue <> ''
                  AND (next_attempt_at IS NULL OR next_attempt_at <= {now})
                ORDER BY created_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var sentCount = 0;
        var senders = new Dictionary<string, ServiceBusSender>(StringComparer.Ordinal);
        using var sendDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        sendDeadline.CancelAfter(BatchSendTimeout);
        try
        {
            foreach (var message in messages)
            {
                if (sendDeadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Messages beyond the deadline were never sent, so they must not consume a retry.
                    break;
                }

                try
                {
                    if (!senders.TryGetValue(message.Queue, out var sender))
                    {
                        sender = serviceBusClient.CreateSender(message.Queue);
                        senders.Add(message.Queue, sender);
                    }

                    var busMessage = new ServiceBusMessage(
                        BinaryData.FromObjectAsJson(message.Payload, JobSerialization.Options))
                    {
                        MessageId = message.Id.ToString("D"),
                        SessionId = message.SessionId,
                        ContentType = "application/json",
                        CorrelationId = TryGetTraceParent(message.Payload)
                    };
                    busMessage.ApplicationProperties["type"] = message.Type;
                    await sender.SendMessageAsync(busMessage, sendDeadline.Token);
                    message.MarkSent(clock.GetUtcNow());
                    sentCount++;
                }
                catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                {
                    RecordFailure(message, exception);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    RecordFailure(message, exception);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return sentCount;
        }
        finally
        {
            foreach (var sender in senders.Values)
            {
                await sender.DisposeAsync();
            }
        }
    }

    private void RecordFailure(OutboxMessageEntity message, Exception exception)
    {
        var permanentlyFailed = message.RecordFailure(clock.GetUtcNow());
        MessageFailure(logger, exception, message.Id, message.Queue, message.Attempts, message.NextAttemptAt);
        if (permanentlyFailed)
        {
            MessageAbandoned(logger, message.Id, message.Attempts);
        }
    }

    private static string? TryGetTraceParent(System.Text.Json.JsonElement payload) =>
        payload.TryGetProperty("traceParent", out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;

    [LoggerMessage(Level = LogLevel.Error, Message = "The outbox relay failed; it will retry after a short delay.")]
    private static partial void RelayFailure(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not relay outbox message {OutboxMessageId} to {Queue}; attempt {Attempt}, next retry at {NextAttemptAt} if any.")]
    private static partial void MessageFailure(
        ILogger logger,
        Exception exception,
        Guid outboxMessageId,
        string queue,
        int attempt,
        DateTimeOffset? nextAttemptAt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {OutboxMessageId} reached its retry limit after {AttemptCount} attempts and will not be sent again.")]
    private static partial void MessageAbandoned(ILogger logger, Guid outboxMessageId, int attemptCount);
}
