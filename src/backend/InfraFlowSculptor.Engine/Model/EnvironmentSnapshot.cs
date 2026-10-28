using System.Collections.Immutable;

namespace InfraFlowSculptor.Engine.Model;

public sealed record EnvironmentSnapshot(string Code, string Region)
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int Position { get; init; }

    public Guid SubscriptionId { get; init; }

    public IReadOnlyDictionary<string, string> Tags { get; init; } =
        ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);
}
