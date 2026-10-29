namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ResourceLinksResponse(
    IReadOnlyList<ResourceLinkResponse> Outgoing,
    IReadOnlyList<ResourceLinkResponse> Incoming);
