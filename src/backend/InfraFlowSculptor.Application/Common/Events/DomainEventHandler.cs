using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Application.Common.Events;

[SuppressMessage("Naming", "CA1711", Justification = "The S-08 public contract names this abstraction DomainEventHandler.")]
public abstract class DomainEventHandler<TEvent> : IDomainEventHandler<TEvent>, IDomainEventHandler
    where TEvent : IDomainEvent
{
    public Type EventType => typeof(TEvent);

    public string HandlerType => GetType().FullName ?? GetType().Name;

    public async Task HandleAsync(
        JsonElement payload,
        DomainEventExecutionContext context,
        CancellationToken cancellationToken)
    {
        var domainEvent = payload.Deserialize<TEvent>(JobSerialization.Options)
            ?? throw new InvalidOperationException($"The payload for event '{typeof(TEvent).FullName}' was empty.");

        await HandleAsync(domainEvent, context, cancellationToken);
    }

    public abstract Task HandleAsync(
        TEvent domainEvent,
        DomainEventExecutionContext context,
        CancellationToken cancellationToken);
}
