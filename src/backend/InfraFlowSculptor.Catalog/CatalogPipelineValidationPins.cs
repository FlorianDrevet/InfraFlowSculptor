namespace InfraFlowSculptor.Catalog;

public sealed record CatalogPipelineValidationPins(
    CatalogPipelineSchemaPin AzurePipelinesSchema,
    string TrivyVersion);
