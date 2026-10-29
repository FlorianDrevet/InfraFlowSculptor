using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ResourceLinkKindOptionResponse(
    LinkKind Kind,
    string LabelFr,
    string LabelEn,
    IReadOnlyList<ResourceLinkFormFieldResponse> Fields,
    IReadOnlyList<ResourceLinkTargetOptionResponse> Targets);
