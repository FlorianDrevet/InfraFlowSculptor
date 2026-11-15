using System.Text.Json.Serialization;

namespace InfraFlowSculptor.Engine.Plan;

public sealed record DeploymentPlan(
    ProjectInfo Project,
    IReadOnlyList<ComponentPlan> Components,
    CatalogPins Pins,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InstallationChecklist? InstallationChecklist = null);
