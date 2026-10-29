using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ResourceLinkIdentityOptionResponse(
    LinkIdentityKind Kind,
    Guid? ResourceId,
    string? ResourceName,
    bool IsDefault);
