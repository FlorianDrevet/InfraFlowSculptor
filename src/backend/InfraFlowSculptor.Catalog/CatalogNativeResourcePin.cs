namespace InfraFlowSculptor.Catalog;

public sealed record CatalogNativeResourcePin(
    string Type,
    string ApiVersion,
    string Reason);
