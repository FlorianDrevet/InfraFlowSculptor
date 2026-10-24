public sealed record DependencyHealthReport(bool IsHealthy, IReadOnlyList<DependencyCheckResult> Checks);
