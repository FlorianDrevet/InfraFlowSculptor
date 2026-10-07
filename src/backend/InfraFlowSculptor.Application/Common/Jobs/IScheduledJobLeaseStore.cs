namespace InfraFlowSculptor.Application.Common.Jobs;

public interface IScheduledJobLeaseStore
{
    Task<bool> TryAcquireAsync(
        string jobName,
        string holder,
        DateTimeOffset now,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task<bool> RenewAsync(
        string jobName,
        string holder,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        string jobName,
        string holder,
        DateTimeOffset nextEligibleAt,
        CancellationToken cancellationToken = default);
}
