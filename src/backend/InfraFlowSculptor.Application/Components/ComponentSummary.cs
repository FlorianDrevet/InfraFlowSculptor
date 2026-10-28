using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Application.Naming;

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
    string? DefaultNameTemplate,
    IReadOnlyList<NamingOverrideValue> NamingOverrides,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<ResourceGroupSummary> ResourceGroups,
    IReadOnlyList<ResourceSummary> Resources);
