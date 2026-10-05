namespace InfraFlowSculptor.Application.Common.Persistence;

public interface IIfsDbTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
