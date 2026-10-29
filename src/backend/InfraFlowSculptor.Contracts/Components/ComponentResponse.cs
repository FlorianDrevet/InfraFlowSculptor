using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Contracts.Naming;

namespace InfraFlowSculptor.Contracts.Components;

public sealed record ComponentResponse(
    Guid Id,
    Guid ProjectId,
    int Version,
    string Name,
    string Code,
    string Description,
    ComponentDeploymentMode Mode,
    bool TargetsAllEnvironments,
    IReadOnlyList<Guid> TargetEnvironmentIds,
    ComponentSingleTargetResponse? SingleTarget,
    RemovedResourceBehavior RemovedResourceBehavior,
    bool DeletionProtectionEnabled,
    DateTimeOffset? FirstPublishedAt,
    string? DefaultNameTemplate,
    IReadOnlyList<NamingOverrideResponse> NamingOverrides,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<ResourceGroupResponse> ResourceGroups,
    IReadOnlyList<ResourceResponse> Resources,
    Guid? DefaultLogAnalyticsResourceId);
