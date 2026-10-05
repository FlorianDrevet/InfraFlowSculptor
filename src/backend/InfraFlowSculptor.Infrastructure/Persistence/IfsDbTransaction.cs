using InfraFlowSculptor.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfraFlowSculptor.Infrastructure.Persistence;

internal sealed class IfsDbTransaction(IDbContextTransaction transaction) : IIfsDbTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
