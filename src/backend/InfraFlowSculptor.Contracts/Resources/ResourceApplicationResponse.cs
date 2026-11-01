namespace InfraFlowSculptor.Contracts.Resources;

public sealed record ResourceApplicationResponse(
    bool Available,
    string CatalogVersion,
    string BuildProfile,
    string? UnavailableReason,
    ApplicationConfigurationResponse? Application);
