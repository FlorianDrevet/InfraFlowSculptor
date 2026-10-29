using System.Collections.Immutable;

namespace InfraFlowSculptor.Engine.Model;

public sealed record EnvironmentSnapshot(string Code, string Region)
{
    public Guid Id { get; init; }

    public int Version { get; init; } = 1;

    public string Name { get; init; } = string.Empty;

    public int Position { get; init; }

    public Guid SubscriptionId { get; init; }

    public bool IsProtected { get; init; }

    public IReadOnlyList<string> Approvers { get; init; } = [];

    public IReadOnlyDictionary<string, string> Tags { get; init; } =
        ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);
}
