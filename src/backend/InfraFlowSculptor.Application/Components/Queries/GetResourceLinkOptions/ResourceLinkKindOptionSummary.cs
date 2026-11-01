using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceLinkOptions;

public sealed record ResourceLinkKindOptionSummary(
    LinkKind Kind,
    string LabelFr,
    string LabelEn,
    IReadOnlyList<LinkFormField> Fields,
    IReadOnlyList<ResourceLinkTargetOptionSummary> Targets);
