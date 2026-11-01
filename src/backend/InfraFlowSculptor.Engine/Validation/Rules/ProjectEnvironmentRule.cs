using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ProjectEnvironmentRule : IValidationRule
{
    public string Code => "VAL-ENV-AUCUN";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Snapshot.Environments.Count > 0
        ? []
        : [context.Finding(
            Code,
            Severity,
            new ObjectRef(ObjectType.Project, context.Snapshot.Project.Id),
            "Le projet ne contient aucun environnement de déploiement.",
            "The project does not contain any deployment environments.")];
}
