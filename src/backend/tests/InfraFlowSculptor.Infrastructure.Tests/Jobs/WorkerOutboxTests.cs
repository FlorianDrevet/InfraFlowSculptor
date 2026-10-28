using System.Text.Json;
using Azure.Messaging.ServiceBus;
using InfraFlowSculptor.Application.Common.Events;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Notifications;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Persistence.Common;
using InfraFlowSculptor.Infrastructure.Tests.Persistence;
using InfraFlowSculptor.Infrastructure.Email;
using InfraFlowSculptor.Worker.Jobs;
using InfraFlowSculptor.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace InfraFlowSculptor.Infrastructure.Tests.Jobs;

public sealed class WorkerOutboxTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public void OutboxMessageStopsRetryingAfterMaximumAttempts()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var createdAt = DateTimeOffset.UtcNow;
        var payload = JsonDocument.Parse("""{"name":"poison"}""").RootElement.Clone();
        var message = new OutboxMessageEntity(new OutboxMessageData(
            Guid.NewGuid(),
            organizationId,
            string.Empty,
            organizationId.Value.ToString("D"),
            "invalid.event.type",
            payload,
            createdAt));

        for (var attempt = 1; attempt < OutboxMessageEntity.MaximumAttempts; attempt++)
        {
            Assert.False(message.RecordFailure(createdAt.AddMinutes(attempt)));
        }

        var finalFailureAt = createdAt.AddMinutes(OutboxMessageEntity.MaximumAttempts);
        Assert.True(message.RecordFailure(finalFailureAt));
        Assert.Equal(OutboxMessageEntity.MaximumAttempts, message.Attempts);
        Assert.Equal(finalFailureAt, message.FailedAt);
        Assert.Null(message.NextAttemptAt);
    }

    [Fact]
    public void OutboxMessageClearsPayloadAfterDelivery()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var payload = JsonDocument.Parse("""{"token":"temporary-secret"}""").RootElement.Clone();
        var message = new OutboxMessageEntity(new OutboxMessageData(
            Guid.NewGuid(),
            organizationId,
            JobQueueNames.Notifications,
            organizationId.Value.ToString("D"),
            "test.notification",
            payload,
            DateTimeOffset.UtcNow));

        message.MarkSent(DateTimeOffset.UtcNow);

        Assert.Equal(JsonValueKind.Null, message.Payload.ValueKind);
    }

    [Fact]
    public void OutboxMessageClearsPayloadAfterPermanentFailure()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var payload = JsonDocument.Parse("""{"token":"temporary-secret"}""").RootElement.Clone();
        var message = new OutboxMessageEntity(new OutboxMessageData(
            Guid.NewGuid(),
            organizationId,
            string.Empty,
            organizationId.Value.ToString("D"),
            "test.event",
            payload,
            DateTimeOffset.UtcNow));

        for (var attempt = 0; attempt < OutboxMessageEntity.MaximumAttempts; attempt++)
        {
            message.RecordFailure(DateTimeOffset.UtcNow.AddMinutes(attempt));
        }

        Assert.Equal(JsonValueKind.Null, message.Payload.ValueKind);
    }

    [Fact]
    public async Task DomainEventDispatcherCallsRegisteredHandlerAndMarksEventProcessed()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var eventId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var payload = JsonDocument.Parse("""{"name":"sample"}""").RootElement.Clone();
        await using (var seedContext = postgres.CreateDbContext(new FixedOrganization(organizationId)))
        {
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.AddOutboxMessage(new OutboxMessageData(
                eventId,
                organizationId,
                string.Empty,
                organizationId.Value.ToString("D"),
                typeof(IDomainEvent).FullName!,
                payload,
                now));
            await seedContext.SaveChangesAsync();
        }

        var handler = new TestDomainEventHandler();
        await using var provider = CreateServiceProvider(handler: handler);
        var dispatcher = new DomainEventDispatcherService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            NullLogger<DomainEventDispatcherService>.Instance);

        await dispatcher.StartAsync(CancellationToken.None);
        var handled = await handler.Handled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilAsync(async () =>
        {
            await using var context = postgres.CreateDbContext(new FixedOrganization(organizationId));
            return await context.OutboxMessages.IgnoreQueryFilters()
                .AnyAsync(item => item.Id == eventId && item.SentAt != null);
        });
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal(eventId, handled.Context.EventId);
        Assert.Equal(organizationId, handled.Context.OrganizationId);
        Assert.Equal("sample", handled.Payload.GetProperty("name").GetString());

        await using var verifyContext = postgres.CreateDbContext(new FixedOrganization(organizationId));
        var message = await verifyContext.OutboxMessages.IgnoreQueryFilters().SingleAsync(item => item.Id == eventId);
        var processed = await verifyContext.ProcessedJobs.SingleAsync(item => item.JobId == eventId);
        Assert.NotNull(message.SentAt);
        Assert.Equal(handler.HandlerType, processed.HandlerType);
    }

    [Fact]
    public async Task InvitationCreatedQueuesLocalizedEmailOnNotificationsQueue()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var invitationId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-10-27T12:00:00Z");
        await using var context = postgres.CreateDbContext(new FixedOrganization(organizationId));
        await context.Database.EnsureCreatedAsync();
        context.Organizations.Add(OrganizationAggregate.Create(
            organizationId,
            "Contoso local",
            new UserProfileId(Guid.NewGuid())).Value);
        await context.SaveChangesAsync();

        var dispatcher = new RecordingJobDispatcher();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [InvitationConfigurationKeys.BaseUrl] = "http://localhost:4200"
            })
            .Build();
        var handler = new InvitationCreatedHandler(
            dispatcher,
            context,
            new EmailSenderOptions("no-reply@infraflowsculptor.local"),
            configuration);
        var domainEvent = new InvitationCreated(
            invitationId,
            organizationId.Value,
            "nina@contoso.example",
            "a-256-bit-random-url-safe-token-value",
            now.AddDays(7),
            "fr");

        await handler.HandleAsync(
            domainEvent,
            new DomainEventExecutionContext(Guid.NewGuid(), organizationId, now),
            CancellationToken.None);

        var queued = Assert.IsType<SendNotificationJob>(dispatcher.Job);
        Assert.Equal(JobQueue.Notifications, dispatcher.Queue);
        Assert.Equal(organizationId, dispatcher.OrganizationId);
        Assert.Equal(NotificationChannel.Email, queued.Channel);
        Assert.Equal("no-reply@infraflowsculptor.local", queued.Email.From);
        Assert.Equal("nina@contoso.example", queued.Email.To);
        Assert.Contains("Contoso local", queued.Email.TextBody, StringComparison.Ordinal);
        Assert.Contains("/invitations/a-256-bit-random-url-safe-token-value", queued.Email.TextBody, StringComparison.Ordinal);
        Assert.Contains("Contoso local", queued.Email.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutboxRelayRecordsFailureAndSchedulesExponentialRetry()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var messageId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var payload = JsonDocument.Parse("""{"jobId":"sample"}""").RootElement.Clone();
        await using (var seedContext = postgres.CreateDbContext(new FixedOrganization(organizationId)))
        {
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.AddOutboxMessage(new OutboxMessageData(
                messageId,
                organizationId,
                JobQueueNames.Generation,
                organizationId.Value.ToString("D"),
                "test.job",
                payload,
                now));
            await seedContext.SaveChangesAsync();
        }

        var sendAttempted = new TaskCompletionSource<DateTimeOffset>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = Substitute.For<ServiceBusSender>();
        sender.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                sendAttempted.TrySetResult(DateTimeOffset.UtcNow);
                return Task.FromException(new InvalidOperationException("Simulated Service Bus send failure."));
            });
        var serviceBusClient = Substitute.For<ServiceBusClient>();
        serviceBusClient.CreateSender(JobQueueNames.Generation).Returns(sender);

        await using var provider = CreateServiceProvider(serviceBusClient: serviceBusClient);
        var relay = new OutboxRelayService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            serviceBusClient,
            TimeProvider.System,
            NullLogger<OutboxRelayService>.Instance);

        await relay.StartAsync(CancellationToken.None);
        var attemptAt = await sendAttempted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilAsync(async () =>
        {
            await using var context = postgres.CreateDbContext(new FixedOrganization(organizationId));
            return await context.OutboxMessages.IgnoreQueryFilters()
                .AnyAsync(item => item.Id == messageId && item.Attempts == 1);
        });
        await relay.StopAsync(CancellationToken.None);

        await using var verifyContext = postgres.CreateDbContext(new FixedOrganization(organizationId));
        var message = await verifyContext.OutboxMessages.IgnoreQueryFilters().SingleAsync(item => item.Id == messageId);
        Assert.Equal(1, message.Attempts);
        Assert.NotNull(message.NextAttemptAt);
        Assert.InRange(message.NextAttemptAt.Value, attemptAt.AddSeconds(2), attemptAt.AddSeconds(2.5));
        Assert.Null(message.SentAt);
    }

    private ServiceProvider CreateServiceProvider(
        ServiceBusClient? serviceBusClient = null,
        IDomainEventHandler? handler = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<WorkerOrganizationContext>();
        services.AddScoped<ICurrentOrganization>(provider => provider.GetRequiredService<WorkerOrganizationContext>());
        services.AddScoped<IfsDbContext>(provider =>
            postgres.CreateDbContext(provider.GetRequiredService<ICurrentOrganization>()));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        if (serviceBusClient is not null)
        {
            services.AddSingleton(serviceBusClient);
        }

        if (handler is not null)
        {
            services.AddSingleton(handler);
            services.AddSingleton<IDomainEventHandler>(handler);
        }

        return services.BuildServiceProvider();
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!await condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), timeout.Token);
        }
    }

    private sealed class TestDomainEventHandler : IDomainEventHandler
    {
        public Type EventType => typeof(IDomainEvent);

        public string HandlerType => "worker-outbox-test-handler";

        public TaskCompletionSource<(JsonElement Payload, DomainEventExecutionContext Context)> Handled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task HandleAsync(
            JsonElement payload,
            DomainEventExecutionContext context,
            CancellationToken cancellationToken)
        {
            Handled.TrySetResult((payload.Clone(), context));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedOrganization(OrganizationId id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }

    private sealed class RecordingJobDispatcher : IJobDispatcher
    {
        public JobQueue Queue { get; private set; }

        public OrganizationId OrganizationId { get; private set; }

        public IJob? Job { get; private set; }

        public Guid Enqueue<TJob>(JobQueue queue, OrganizationId organizationId, TJob job)
            where TJob : IJob
        {
            Queue = queue;
            OrganizationId = organizationId;
            Job = job;
            return Guid.NewGuid();
        }
    }
}
