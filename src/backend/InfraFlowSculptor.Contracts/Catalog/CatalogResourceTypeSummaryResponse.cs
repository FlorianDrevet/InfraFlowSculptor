namespace InfraFlowSculptor.Contracts.Catalog;

public sealed record CatalogResourceTypeSummaryResponse(
    string Type,
    string Category,
    string LabelFr,
    string LabelEn,
    string Icon);
