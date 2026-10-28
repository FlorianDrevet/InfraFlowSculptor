using InfraFlowSculptor.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfraFlowSculptor.Infrastructure.Persistence;

internal sealed class IfsDbTransaction(
    IDbContextTransaction transaction,
    bool isOwner,
    string? savepointName) : IIfsDbTransaction
{
    public bool IsOwner { get; } = isOwner;

    private bool completed;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (IsOwner)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else if (savepointName is not null)
        {
            await transaction.ReleaseSavepointAsync(savepointName, cancellationToken);
        }

        completed = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (IsOwner)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        else if (savepointName is not null)
        {
            await transaction.RollbackToSavepointAsync(savepointName, cancellationToken);
            await transaction.ReleaseSavepointAsync(savepointName, cancellationToken);
        }

        completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (IsOwner)
        {
            await transaction.DisposeAsync();
        }
        else if (!completed && savepointName is not null)
        {
            await RollbackAsync(CancellationToken.None);
        }
    }
}
