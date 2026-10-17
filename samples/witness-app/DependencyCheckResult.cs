public sealed record DependencyCheckResult(string Name, bool? Ok, int DurationMs, string? Error);
