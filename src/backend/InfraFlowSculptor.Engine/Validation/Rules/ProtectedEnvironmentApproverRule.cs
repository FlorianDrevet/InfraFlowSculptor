using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ProtectedEnvironmentApproverRule : IValidationRule
{
    public string Code => "VAL-ENV-APPROBATEURS";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Snapshot.Environments
        .Where(environment => environment.IsProtected && environment.Approvers.Count == 0)
        .Select(environment => context.Finding(
            Code,
            Severity,
            new ObjectRef(ObjectType.Environment, environment.Id),
            $"L’environnement protégé « {environment.Name} » n’a aucun approbateur.",
            $"Protected environment '{environment.Name}' has no approver.",
            environment.Id))
        .ToArray();
}
