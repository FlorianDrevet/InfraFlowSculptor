using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class SystemTagsDisabledRule : IValidationRule
{
    public string Code => "VAL-PRJ-TAGS-SYSTEME";

    public FindingSeverity Severity => FindingSeverity.Warning;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Snapshot.Project.SystemTagsEnabled
        ? []
        : [context.Finding(
            Code,
            Severity,
            new ObjectRef(ObjectType.Project, context.Snapshot.Project.Id),
            "Les tags système sont désactivés; les ressources créées ne porteront pas les tags de suivi IFS.",
            "System tags are disabled; created resources will not carry IFS tracking tags.")];
}
