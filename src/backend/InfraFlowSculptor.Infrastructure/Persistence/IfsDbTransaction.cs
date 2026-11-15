using InfraFlowSculptor.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfraFlowSculptor.Infrastructure.Persistence;

internal sealed class IfsDbTransaction(
    IDbContextTransaction transaction,
    bool isOwner,
    string? savepointName,
    ITransactionRollbackActions? rollbackActions,
    int rollbackCheckpoint) : IIfsDbTransaction
{
    public bool IsOwner { get; } = isOwner;

    private bool completed;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsOwner)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else if (savepointName is not null)
            {
                await transaction.ReleaseSavepointAsync(savepointName, cancellationToken);
            }
        }
        catch
        {
            // Commit outcome is uncertain; disposal must not roll back or delete a possibly referenced secret.
            completed = true;
            throw;
        }

        if (IsOwner)
        {
            rollbackActions?.Commit();
        }

        completed = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (completed)
        {
            return;
        }

        try
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
        }
        catch
        {
            // The outcome is uncertain; avoid a second explicit rollback during disposal masking the command failure.
            completed = true;
            throw;
        }

        // A failed SQL rollback leaves commit state uncertain; deleting a staged secret then could break a committed reference.
        await (rollbackActions?.RollbackAsync(rollbackCheckpoint, CancellationToken.None)
            ?? Task.CompletedTask);
        completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (IsOwner)
        {
            try
            {
                if (!completed)
                {
                    await RollbackAsync(CancellationToken.None);
                }
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        }
        else if (!completed && savepointName is not null)
        {
            await RollbackAsync(CancellationToken.None);
        }
    }
}
