namespace InfraFlowSculptor.Catalog;

public sealed record CatalogRole(
    string Name,
    string DefinitionId,
    string Description,
    string DocumentationUrl);
