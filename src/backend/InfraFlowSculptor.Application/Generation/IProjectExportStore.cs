namespace InfraFlowSculptor.Application.Generation;

public interface IProjectExportStore
{
    Task<string> SaveAsync(
        ReadOnlyMemory<byte> content,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task<Stream?> OpenAsync(
        string token,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
