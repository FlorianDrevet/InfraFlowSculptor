using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Application.Common.Events;

[SuppressMessage("Naming", "CA1711", Justification = "The S-08 public contract names this interface IDomainEventHandler.")]
public interface IDomainEventHandler
{
    Type EventType { get; }

    string HandlerType { get; }

    Task HandleAsync(
        JsonElement payload,
        DomainEventExecutionContext context,
        CancellationToken cancellationToken);
}
