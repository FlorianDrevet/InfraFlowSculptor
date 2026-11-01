namespace InfraFlowSculptor.Application.Components.Queries.GetResourceLinks;

public sealed record ResourceLinksSummary(
    IReadOnlyList<ResourceLinkSummary> Outgoing,
    IReadOnlyList<ResourceLinkSummary> Incoming);
