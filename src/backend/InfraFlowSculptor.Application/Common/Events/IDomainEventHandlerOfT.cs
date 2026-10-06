using System.Diagnostics.CodeAnalysis;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Application.Common.Events;

[SuppressMessage("Naming", "CA1711", Justification = "The S-08 public contract names this interface IDomainEventHandler<TEvent>.")]
public interface IDomainEventHandler<TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(
        TEvent domainEvent,
        DomainEventExecutionContext context,
        CancellationToken cancellationToken);
}
