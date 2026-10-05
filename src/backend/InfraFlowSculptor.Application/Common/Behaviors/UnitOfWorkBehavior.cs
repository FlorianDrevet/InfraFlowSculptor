using System.Text.Json;
using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using Mediator;

namespace InfraFlowSculptor.Application.Common.Behaviors;

public sealed class UnitOfWorkBehavior<TMessage, TResponse>(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    TimeProvider clock)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!IsCommand())
        {
            return await next(message, cancellationToken);
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next(message, cancellationToken);
            if (response is IErrorOr { IsError: true })
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ClearDomainEvents();
                return response;
            }

            var organizationId = currentOrganization.Id;
            foreach (var domainEvent in dbContext.GetDomainEvents())
            {
                if (organizationId is null)
                {
                    throw new InvalidOperationException(
                        "A command that raises domain events requires an active organization.");
                }

                dbContext.AddOutboxMessage(new OutboxMessageData(
                    Guid.CreateVersion7(clock.GetUtcNow()),
                    organizationId.Value,
                    Queue: string.Empty,
                    SessionId: organizationId.Value.Value.ToString("D"),
                    Type: domainEvent.GetType().FullName ?? domainEvent.GetType().Name,
                    Payload: JsonSerializer.SerializeToElement(domainEvent, domainEvent.GetType()),
                    CreatedAt: clock.GetUtcNow()));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            dbContext.ClearDomainEvents();
            return response;
        }
        catch (ConcurrencyConflictException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ClearDomainEvents();
            return (dynamic)new List<Error> { Error.Conflict("CONFLICT_VERSION") };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ClearDomainEvents();
            throw;
        }
    }

    private static bool IsCommand() =>
        typeof(ICommand).IsAssignableFrom(typeof(TMessage))
        || typeof(TMessage).GetInterfaces().Any(@interface =>
            @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(ICommand<>));
}
