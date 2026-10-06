using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using InfraFlowSculptor.Application.Common.Observability;
using Azure.Messaging.ServiceBus;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Worker.Jobs;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Worker.Services;

public sealed partial class SessionJobProcessorService(
    IServiceScopeFactory scopeFactory,
    ServiceBusClient serviceBusClient,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    TimeProvider clock,
    ILogger<SessionJobProcessorService> logger) : BackgroundService
{
    private const int MaximumDeliveryAttempts = 10;
    private const string RetryCountApplicationProperty = "ifsRetryCount";
    private readonly List<ServiceBusSessionProcessor> processors = [];
    private int MaxConcurrentSessions => Math.Max(1, configuration.GetValue("Jobs:MaxConcurrentSessions", 8));
    private int DefaultJobsPerMinute => configuration.GetValue("Jobs:DefaultJobsPerMinute", 1000);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (processors.Count == 0)
        {
            foreach (var queueName in JobQueueNames.All)
            {
                var processor = serviceBusClient.CreateSessionProcessor(
                    queueName,
                    new ServiceBusSessionProcessorOptions
                    {
                        MaxConcurrentSessions = MaxConcurrentSessions,
                        MaxConcurrentCallsPerSession = 1,
                        SessionIdleTimeout = TimeSpan.FromSeconds(5),
                        AutoCompleteMessages = false
                    });
                processor.ProcessMessageAsync += args => ProcessMessageAsync(queueName, args);
                processor.ProcessErrorAsync += ProcessErrorAsync;
                processors.Add(processor);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.WhenAll(processors.Select(processor => processor.StartProcessingAsync(stoppingToken)));
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                ProcessorStartFailure(logger, exception);
                await StopProcessorsAsync(CancellationToken.None);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        await StopProcessorsAsync(CancellationToken.None);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await StopProcessorsAsync(cancellationToken);
        foreach (var processor in processors)
        {
            await processor.DisposeAsync();
        }
    }

    private async Task ProcessMessageAsync(string queueName, ProcessSessionMessageEventArgs args)
    {
        OrganizationId? organizationId = null;
        string? traceId = null;
        try
        {
            var envelope = JsonSerializer.Deserialize<JobEnvelope>(args.Message.Body.ToString(), JobSerialization.Options)
                ?? throw new JsonException("The Service Bus job envelope was empty.");
            organizationId = envelope.OrganizationId;

            await using var scope = scopeFactory.CreateAsyncScope();
            var organization = scope.ServiceProvider.GetRequiredService<WorkerOrganizationContext>();
            organization.Set(envelope.OrganizationId);
            var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
            var handler = scope.ServiceProvider.GetServices<IJobHandler>()
                .SingleOrDefault(candidate => candidate.JobType.FullName == envelope.Type);
            if (handler is null)
            {
                await args.DeadLetterMessageAsync(
                    args.Message,
                    "UnknownJobType",
                    $"No job handler is registered for '{envelope.Type}'.");
                return;
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            var handlerName = handler.HandlerType;
            var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO processed_jobs (job_id, handler_type, processed_at)
                VALUES ({envelope.JobId}, {handlerName}, {clock.GetUtcNow()})
                ON CONFLICT (job_id, handler_type) DO NOTHING
                """);

            if (inserted == 0)
            {
                await transaction.CommitAsync();
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            var budgetStore = scope.ServiceProvider.GetRequiredService<IOrganizationJobBudgetStore>();
            if (!await budgetStore.TryConsumeAsync(
                    envelope.OrganizationId,
                    DefaultJobsPerMinute,
                    clock.GetUtcNow()))
            {
                await transaction.RollbackAsync();
                await DeferMessageAsync(queueName, args);
                return;
            }

            ActivityContext parentContext = default;
            if (!string.IsNullOrWhiteSpace(envelope.TraceParent))
            {
                ActivityContext.TryParse(envelope.TraceParent, null, out parentContext);
            }

            using var activity = IfsTelemetry.ActivitySource.StartActivity(
                "job.process",
                ActivityKind.Consumer,
                parentContext);
            activity?.SetTag("job.id", envelope.JobId);
            activity?.SetTag("job.type", envelope.Type);
            IfsTelemetry.SetCurrentActivityTags(envelope.OrganizationId);
            traceId = activity?.TraceId.ToString()
                ?? (parentContext.TraceId == default ? null : parentContext.TraceId.ToString());
            using var logScope = IfsTelemetry.BeginLogScope(logger, envelope.OrganizationId, traceId: traceId);

            await handler.HandleAsync(
                envelope.Payload,
                new JobExecutionContext(envelope.JobId, envelope.OrganizationId, clock.GetUtcNow()),
                args.CancellationToken);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            IfsTelemetry.RecordJob(envelope.OrganizationId);
            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            using var failureScope = IfsTelemetry.BeginLogScope(logger, organizationId, traceId: traceId);
            MessageFailure(logger, exception, args.Message.MessageId);
            try
            {
                await RetryFailedMessageAsync(queueName, args, exception);
            }
            catch (Exception settlementException)
            {
                SettlementFailure(logger, settlementException, args.Message.MessageId);
            }
        }
        finally
        {
            try
            {
                args.ReleaseSession();
            }
            catch (InvalidOperationException)
            {
                // The session lock may already have expired while a slow handler was running.
            }
        }
    }

    private async Task RetryFailedMessageAsync(
        string queueName,
        ProcessSessionMessageEventArgs args,
        Exception exception)
    {
        var retryCount = args.Message.ApplicationProperties.TryGetValue(
                RetryCountApplicationProperty,
                out var retryCountValue)
            && int.TryParse(
                Convert.ToString(retryCountValue, CultureInfo.InvariantCulture),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsedRetryCount)
                ? parsedRetryCount
                : 0;
        retryCount = Math.Max(0, retryCount);

        if (args.Message.DeliveryCount >= MaximumDeliveryAttempts
            || retryCount >= MaximumDeliveryAttempts - 1)
        {
            await args.DeadLetterMessageAsync(args.Message, "MaxDeliveryCountExceeded", exception.Message);
            return;
        }

        var nextRetryCount = retryCount + 1;
        var retryDelay = TimeSpan.FromSeconds(Math.Min(1 << Math.Min(retryCount, 8), 300));
        var retryMessage = new ServiceBusMessage(args.Message.Body)
        {
            MessageId = Guid.CreateVersion7(clock.GetUtcNow()).ToString("D"),
            SessionId = args.Message.SessionId,
            ContentType = args.Message.ContentType,
            CorrelationId = args.Message.CorrelationId,
            Subject = args.Message.Subject
        };
        foreach (var property in args.Message.ApplicationProperties)
        {
            retryMessage.ApplicationProperties[property.Key] = property.Value;
        }

        retryMessage.ApplicationProperties[RetryCountApplicationProperty] = nextRetryCount;
        await using var sender = serviceBusClient.CreateSender(queueName);
        await sender.ScheduleMessageAsync(retryMessage, clock.GetUtcNow().Add(retryDelay));
        await args.CompleteMessageAsync(args.Message);
        MessageRetryScheduled(logger, args.Message.MessageId, nextRetryCount, retryDelay);
    }

    private async Task DeferMessageAsync(string queueName, ProcessSessionMessageEventArgs args)
    {
        var scheduledEnqueueTime = clock.GetUtcNow().AddSeconds(30);
        var deferred = new ServiceBusMessage(args.Message.Body)
        {
            MessageId = Guid.CreateVersion7(clock.GetUtcNow()).ToString("D"),
            SessionId = args.Message.SessionId,
            ContentType = args.Message.ContentType,
            CorrelationId = args.Message.CorrelationId,
            Subject = args.Message.Subject
        };
        foreach (var property in args.Message.ApplicationProperties)
        {
            deferred.ApplicationProperties[property.Key] = property.Value;
        }

        await using var sender = serviceBusClient.CreateSender(queueName);
        await sender.ScheduleMessageAsync(deferred, scheduledEnqueueTime);
        await args.CompleteMessageAsync(args.Message);
    }

    private async Task StopProcessorsAsync(CancellationToken cancellationToken)
    {
        foreach (var processor in processors.Where(processor => processor.IsProcessing))
        {
            try
            {
                await processor.StopProcessingAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                StopFailure(logger, exception);
            }
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        if (ServiceBusProcessorErrorClassifier.IsExpectedLocalEmulatorIdleAcceptSessionFailure(
                ServiceBusProcessorErrorClassifier.IsLocalServiceBusEmulator(
                    configuration.GetValue<bool>("Ifs:ServiceBus:IsEmulator"),
                    hostEnvironment.IsDevelopment(),
                    serviceBusClient.FullyQualifiedNamespace),
                args.ErrorSource,
                args.Exception))
        {
            EmulatorIdleAcceptSessionLinkClosed(logger, args.Exception, args.EntityPath);
            return Task.CompletedTask;
        }

        ProcessorError(logger, args.Exception, args.EntityPath, args.ErrorSource.ToString());
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Service Bus session processors could not start; retrying in five seconds.")]
    private static partial void ProcessorStartFailure(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process Service Bus message {MessageId}.")]
    private static partial void MessageFailure(ILogger logger, Exception exception, string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not settle failed Service Bus message {MessageId}.")]
    private static partial void SettlementFailure(ILogger logger, Exception exception, string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled Service Bus message {MessageId} for retry {RetryCount} after {RetryDelay}.")]
    private static partial void MessageRetryScheduled(
        ILogger logger,
        string messageId,
        int retryCount,
        TimeSpan retryDelay);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not stop a Service Bus session processor cleanly.")]
    private static partial void StopFailure(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service Bus session processor error on {EntityPath} ({ErrorSource}).")]
    private static partial void ProcessorError(
        ILogger logger,
        Exception exception,
        string entityPath,
        string errorSource);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Local Service Bus emulator closed an idle session-accept AMQP link on {EntityPath}.")]
    private static partial void EmulatorIdleAcceptSessionLinkClosed(
        ILogger logger,
        Exception exception,
        string entityPath);
}
