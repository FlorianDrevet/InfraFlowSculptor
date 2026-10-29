using System.Collections.Immutable;

namespace InfraFlowSculptor.Engine.Model;

public sealed record ComponentSnapshot(
    Guid Id,
    string Name,
    string Code,
    string Mode,
    IReadOnlyList<Guid> TargetEnvironmentIds,
    ComponentSingleTargetSnapshot? SingleTarget,
    IReadOnlyList<ResourceGroupSnapshot> ResourceGroups,
    IReadOnlyList<ResourceSnapshot> Resources)
{
    public int Version { get; init; } = 1;

    public bool TargetsAllEnvironments { get; init; }

    public ComponentNamingSnapshot? Naming { get; init; }

    public Guid? DefaultLogAnalyticsResourceId { get; init; }

    public IReadOnlyDictionary<string, string> Tags { get; init; } =
        ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);
}
