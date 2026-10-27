namespace InfraFlowSculptor.Application.Common.Persistence;

public interface IIfsDbTransaction : IAsyncDisposable
{
    bool IsOwner { get; }

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
