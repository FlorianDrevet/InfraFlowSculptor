using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ResourceLinkTargetOptionResponse(
    Guid ComponentId,
    string ComponentCode,
    Guid ResourceId,
    string ResourceName,
    string ResourceType,
    bool IsExisting,
    IReadOnlyList<string> Roles,
    IReadOnlyList<DataAccessLevel> DataAccessLevels,
    IReadOnlyList<ResourceLinkIdentityOptionResponse> Identities);
