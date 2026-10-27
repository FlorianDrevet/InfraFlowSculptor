using InfraFlowSculptor.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfraFlowSculptor.Infrastructure.Persistence;

internal sealed class IfsDbTransaction(IDbContextTransaction transaction, bool isOwner) : IIfsDbTransaction
{
    public bool IsOwner { get; } = isOwner;

    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        IsOwner ? transaction.CommitAsync(cancellationToken) : Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        IsOwner ? transaction.RollbackAsync(cancellationToken) : Task.CompletedTask;

    public ValueTask DisposeAsync() => IsOwner ? transaction.DisposeAsync() : ValueTask.CompletedTask;
}
