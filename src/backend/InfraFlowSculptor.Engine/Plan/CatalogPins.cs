using InfraFlowSculptor.Catalog;

namespace InfraFlowSculptor.Engine.Plan;

public sealed record CatalogPins(
    string Version,
    string Fingerprint,
    string BicepCliVersion,
    CatalogStartupImagePin StartupImage,
    IReadOnlyDictionary<string, CatalogModulePin> Modules,
    IReadOnlyDictionary<string, string> PowerShellModules,
    CatalogPipelineValidationPins PipelineValidation,
    IReadOnlyDictionary<string, CatalogNativeResourcePin> NativeResources);
