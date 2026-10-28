using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Application.Components;

public sealed record ComponentSummary(
    Guid Id,
    Guid ProjectId,
    int Version,
    string Name,
    string Code,
    string Description,
    ComponentDeploymentMode Mode,
    bool TargetsAllEnvironments,
    IReadOnlyList<Guid> TargetEnvironmentIds,
    ComponentSingleTargetSummary? SingleTarget,
    RemovedResourceBehavior RemovedResourceBehavior,
    bool DeletionProtectionEnabled,
    DateTimeOffset? FirstPublishedAt,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<ResourceGroupSummary> ResourceGroups,
    IReadOnlyList<ResourceSummary> Resources);
