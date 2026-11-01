using System.Text.Json.Serialization;

namespace InfraFlowSculptor.Engine.Plan;

public sealed record ReleaseDataPlan(
    string Schema,
    string Project,
    string Component,
    string Target,
    string Unit,
    Guid SubscriptionId,
    string Location,
    bool Protected,
    string? DeploymentIdentityObjectId,
    IReadOnlyList<ReleaseDependencyPlan> Dependencies,
    IReadOnlyList<ReleaseSecretWritePlan> SecretWrites,
    IReadOnlyList<ReleaseSecretReferencePlan> SecretReferences,
    IReadOnlyList<ReleaseAppOwnedStatePlan> AppOwnedState,
    IReadOnlyList<ReleaseDataAccessPlan> DataAccess,
    IReadOnlyList<string> AccessObjectTypes,
    IReadOnlyList<string> RestartOnSecretChange,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? ExplicitAccessRevocations = null);
