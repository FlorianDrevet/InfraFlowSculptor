using Microsoft.Extensions.Logging;

namespace InfraFlowSculptor.Application.Common.Persistence;

public sealed partial class TransactionRollbackActions(
    ILogger<TransactionRollbackActions> logger) : ITransactionRollbackActions
{
    private readonly List<Func<CancellationToken, Task>> actions = [];

    public void Register(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        actions.Add(action);
    }

    public int CaptureCheckpoint() => actions.Count;

    public async Task RollbackAsync(int checkpoint, CancellationToken cancellationToken)
    {
        if (checkpoint < 0 || checkpoint > actions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        var rollbackActions = actions.Skip(checkpoint).Reverse().ToArray();
        actions.RemoveRange(checkpoint, actions.Count - checkpoint);
        foreach (var action in rollbackActions)
        {
            try
            {
                await action(cancellationToken);
            }
            catch (Exception)
            {
                LogRollbackCleanupFailure(logger);
            }
        }
    }

    public void Commit() => actions.Clear();

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "A transaction rollback cleanup failed; an unreferenced secret may remain in the secret store.")]
    private static partial void LogRollbackCleanupFailure(ILogger logger);
}
