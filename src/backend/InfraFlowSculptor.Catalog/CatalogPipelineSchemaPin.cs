namespace InfraFlowSculptor.Catalog;

public sealed record CatalogPipelineSchemaPin(
    string Repository,
    string Commit,
    string Path,
    string Sha256,
    string Source);
