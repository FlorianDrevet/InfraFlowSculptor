namespace InfraFlowSculptor.Catalog;

public sealed record CatalogVersionPins(
    string BicepCliVersion,
    CatalogStartupImagePin StartupImage,
    IReadOnlyDictionary<string, CatalogModulePin> Modules,
    IReadOnlyDictionary<string, string> PowerShellModules,
    CatalogPipelineValidationPins PipelineValidation,
    IReadOnlyDictionary<string, CatalogNativeResourcePin> NativeResources);
