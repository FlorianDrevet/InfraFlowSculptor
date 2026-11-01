using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Validation;

namespace InfraFlowSculptor.Application.Components.Validation;

public sealed record FindingFixContext(
    Guid ProjectId,
    ModelSnapshot Snapshot,
    CatalogVersion Catalog,
    ValidationResult Validation,
    ValidationFinding Finding);
