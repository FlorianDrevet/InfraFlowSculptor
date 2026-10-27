namespace InfraFlowSculptor.Contracts.Catalog;

public sealed record CatalogVersionResponse(
    string Version,
    string Status,
    DateOnly PublishedAt,
    DateOnly? SupportEndsAt,
    string Fingerprint);
