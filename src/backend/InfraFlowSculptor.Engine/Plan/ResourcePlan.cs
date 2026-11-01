namespace InfraFlowSculptor.Engine.Plan;

public sealed record ResourcePlan(
    string Type,
    string ArmType,
    string LogicalName,
    string? Name,
    string? ResourceGroup,
    string Region,
    bool Present,
    string? ResourceId,
    IReadOnlyList<ResourcePropertyPlan> Properties,
    IReadOnlyList<ResourcePropertyPlan> FixedValues,
    IReadOnlyList<ResourceChildPlan> Children,
    IReadOnlyList<ResourceIdentityPlan> Identities,
    ExposurePlan? Exposure,
    IReadOnlyDictionary<string, string> Tags,
    bool IsExisting,
    string? ExistingId);
