using System.Text.Json;
using InfraFlowSculptor.Application.Common.Events;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Application.Tests.Jobs;

public sealed class DomainEventHandlerTests
{
    [Fact]
    public async Task GenericHandlerReceivesTheDeserializedEventAndExecutionContext()
    {
        var expectedEvent = new TestEvent("created");
        var expectedContext = new DomainEventExecutionContext(
            Guid.NewGuid(),
            new OrganizationId(Guid.NewGuid()),
            DateTimeOffset.UtcNow);
        var handler = new TestEventHandler();
        IDomainEventHandler dispatcher = handler;

        await dispatcher.HandleAsync(
            JsonSerializer.SerializeToElement(expectedEvent, JobSerialization.Options),
            expectedContext,
            CancellationToken.None);

        Assert.Equal(expectedEvent, handler.ReceivedEvent);
        Assert.Equal(expectedContext, handler.ReceivedContext);
    }

    private sealed record TestEvent(string Name) : IDomainEvent;

    private sealed class TestEventHandler : DomainEventHandler<TestEvent>
    {
        public TestEvent? ReceivedEvent { get; private set; }

        public DomainEventExecutionContext? ReceivedContext { get; private set; }

        public override Task HandleAsync(
            TestEvent domainEvent,
            DomainEventExecutionContext context,
            CancellationToken cancellationToken)
        {
            ReceivedEvent = domainEvent;
            ReceivedContext = context;
            return Task.CompletedTask;
        }
    }
}
