namespace InfraFlowSculptor.Application.Common.Persistence;

public interface IIdempotencyStore
{
    Task<IIfsDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task<IdempotencyEntry?> FindAsync(
        IdempotencyScope scope,
        Guid key,
        CancellationToken cancellationToken = default);

    Task<bool> TryStartAsync(
        IdempotencyScope scope,
        Guid key,
        string requestHash,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        IdempotencyScope scope,
        Guid key,
        IdempotencyResponse response,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        IdempotencyScope scope,
        Guid key,
        CancellationToken cancellationToken = default);
}
