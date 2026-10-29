using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceLinkOptions;

public sealed record ResourceLinkTargetOptionSummary(
    Guid ComponentId,
    string ComponentCode,
    Guid ResourceId,
    string ResourceName,
    string ResourceType,
    bool IsExisting,
    IReadOnlyList<string> Roles,
    IReadOnlyList<DataAccessLevel> DataAccessLevels,
    IReadOnlyList<ResourceLinkIdentityOptionSummary> Identities);
