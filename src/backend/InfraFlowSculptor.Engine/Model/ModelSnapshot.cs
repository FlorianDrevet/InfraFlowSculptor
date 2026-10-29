using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Model;

public sealed record ModelSnapshot(
    ProjectSnapshot Project,
    IReadOnlyList<EnvironmentSnapshot> Environments,
    NamingConventionSnapshot Naming,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<ComponentSnapshot> Components)
{
    public IReadOnlyDictionary<string, NamingRuleSnapshot> NamingRules { get; init; } =
        new Dictionary<string, NamingRuleSnapshot>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> RegionCodes { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<GlobalNameReservationSnapshot> OrganizationGlobalNames { get; init; } = [];

    public IReadOnlyList<LinkSnapshot> Links { get; init; } = [];

    public IReadOnlyList<AppSettingSnapshot> AppSettings { get; init; } = [];

    public IReadOnlyList<ApplicationSnapshot> Applications { get; init; } = [];

    public IReadOnlyDictionary<ObjectRef, int> ObjectVersions { get; init; } = new Dictionary<ObjectRef, int>();
}
