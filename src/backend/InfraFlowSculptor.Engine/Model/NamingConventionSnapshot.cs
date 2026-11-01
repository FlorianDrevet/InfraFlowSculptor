using System.Collections.Immutable;

namespace InfraFlowSculptor.Engine.Model;

public sealed record NamingConventionSnapshot(string Preset, string DefaultTemplate, string ResourceGroupTemplate)
{
    public IReadOnlyDictionary<string, string> TypeTemplates { get; init; } =
        ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> TypeAbbreviations { get; init; } =
        ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);
}
