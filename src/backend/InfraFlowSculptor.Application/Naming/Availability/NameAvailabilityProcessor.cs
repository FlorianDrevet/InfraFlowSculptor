namespace InfraFlowSculptor.Application.Naming.Availability;

public sealed class NameAvailabilityProcessor(
    INameAvailabilityStore store,
    INameAvailabilityProbe probe,
    TimeProvider clock)
{
    private const int BatchLimit = 20;
    private const int MaximumUnknownAttempts = 3;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan UnknownCacheDuration = TimeSpan.FromHours(1);

    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        await store.DeleteExpiredAsync(now, cancellationToken);
        var pending = await store.FindPendingAsync(now, BatchLimit, cancellationToken);
        foreach (var request in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NameAvailabilityProbeResult result;
            try
            {
                result = await probe.ProbeAsync(
                    request.AvailabilityDomain,
                    request.Name,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                result = new NameAvailabilityProbeResult(
                    NameAvailabilityOutcome.Unknown,
                    NameAvailabilityFailure.DnsError);
            }

            var attemptedAt = clock.GetUtcNow();
            var update = CreateUpdate(request.Attempts, result, attemptedAt);
            await store.RecordAttemptAsync(
                request.AvailabilityDomain,
                request.Name,
                update,
                cancellationToken);
        }
    }

    private static NameAvailabilityUpdate CreateUpdate(
        int previousAttempts,
        NameAvailabilityProbeResult result,
        DateTimeOffset now) => result.Outcome switch
    {
        NameAvailabilityOutcome.Resolved => new NameAvailabilityUpdate(
            NameAvailabilityState.Resolved,
            now.Add(CacheDuration),
            now,
            now.Add(CacheDuration),
            0,
            null),
        NameAvailabilityOutcome.NotResolved => new NameAvailabilityUpdate(
            NameAvailabilityState.NotResolved,
            now.Add(CacheDuration),
            now,
            now.Add(CacheDuration),
            0,
            null),
        NameAvailabilityOutcome.Unknown => CreateUnknownUpdate(previousAttempts, result.Failure, now),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown name availability outcome.")
    };

    private static NameAvailabilityUpdate CreateUnknownUpdate(
        int previousAttempts,
        NameAvailabilityFailure? failure,
        DateTimeOffset now)
    {
        var attempts = previousAttempts + 1;
        if (attempts >= MaximumUnknownAttempts)
        {
            var expiresAt = now.Add(UnknownCacheDuration);
            return new NameAvailabilityUpdate(
                NameAvailabilityState.Unknown,
                expiresAt,
                null,
                expiresAt,
                attempts,
                failure ?? NameAvailabilityFailure.DnsError);
        }

        var retryDelay = attempts == 1 ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(5);
        return new NameAvailabilityUpdate(
            NameAvailabilityState.Pending,
            now.Add(retryDelay),
            null,
            null,
            attempts,
            failure ?? NameAvailabilityFailure.DnsError);
    }
}
