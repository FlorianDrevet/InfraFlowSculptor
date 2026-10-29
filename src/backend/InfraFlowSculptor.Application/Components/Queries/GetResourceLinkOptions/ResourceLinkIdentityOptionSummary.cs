using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceLinkOptions;

public sealed record ResourceLinkIdentityOptionSummary(
    LinkIdentityKind Kind,
    Guid? ResourceId,
    string? ResourceName,
    bool IsDefault);
