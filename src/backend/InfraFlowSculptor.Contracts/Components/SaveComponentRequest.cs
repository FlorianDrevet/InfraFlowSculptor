using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Contracts.Components;

public sealed record SaveComponentRequest(
    string Name,
    string Code,
    string Description,
    ComponentDeploymentMode Mode,
    bool TargetsAllEnvironments,
    IReadOnlyList<Guid> TargetEnvironmentIds,
    SingleComponentTargetRequest? SingleTarget,
    RemovedResourceBehavior RemovedResourceBehavior,
    bool DeletionProtectionEnabled,
    IReadOnlyDictionary<string, string> Tags,
    int ExpectedVersion);
