namespace InfraFlowSculptor.Application.Naming.Availability;

public interface INameAvailabilityStore
{
    Task<IReadOnlyDictionary<string, NameAvailabilityCacheEntry>> FindManyAsync(
        string availabilityDomain,
        IReadOnlyList<string> names,
        CancellationToken cancellationToken = default);

    Task EnqueueIfMissingOrExpiredAsync(
        string availabilityDomain,
        IReadOnlyList<string> names,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default);

    Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingNameAvailability>> FindPendingAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default);

    Task RecordAttemptAsync(
        string availabilityDomain,
        string name,
        NameAvailabilityUpdate update,
        CancellationToken cancellationToken = default);
}
