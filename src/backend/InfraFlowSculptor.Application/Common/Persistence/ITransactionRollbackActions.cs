namespace InfraFlowSculptor.Application.Common.Persistence;

public interface ITransactionRollbackActions
{
    void Register(Func<CancellationToken, Task> action);

    int CaptureCheckpoint();

    Task RollbackAsync(int checkpoint, CancellationToken cancellationToken);

    void Commit();
}
