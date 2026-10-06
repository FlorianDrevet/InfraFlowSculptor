using System.Diagnostics;

public sealed class DependencyHealthService(IEnumerable<IDependencyCheck> checks)
{
    public async Task<DependencyHealthReport> RunAsync(CancellationToken cancellationToken)
    {
        var results = new List<DependencyCheckResult>();

        foreach (var check in checks)
        {
            var startedAt = Stopwatch.GetTimestamp();
            try
            {
                var outcome = await check.CheckAsync(cancellationToken);
                results.Add(outcome is null
                    ? new DependencyCheckResult(check.Name, null, GetElapsedMilliseconds(startedAt), "skipped")
                    : new DependencyCheckResult(
                        check.Name,
                        outcome.Ok,
                        GetElapsedMilliseconds(startedAt),
                        outcome.Ok ? null : outcome.Error ?? "dependency check failed"));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                results.Add(new DependencyCheckResult(
                    check.Name,
                    false,
                    GetElapsedMilliseconds(startedAt),
                    "dependency check failed"));
            }
        }

        return new DependencyHealthReport(results.All(result => result.Ok is not false), results);
    }

    private static int GetElapsedMilliseconds(long startedAt) =>
        (int)Math.Clamp(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 0, int.MaxValue);
}
