using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Engine.Applications;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceApplication;

public sealed record ResourceApplicationSummary(
    bool Available,
    string CatalogVersion,
    string BuildProfile,
    string? UnavailableReason,
    ApplicationAggregate? SavedApplication,
    ApplicationDefinition? Definition,
    BuildRegistryResolution? BuildRegistry,
    IReadOnlyList<CatalogPipelineStepDescriptor> StepDescriptors,
    IReadOnlyList<ApplicationStepFinding> Findings);
