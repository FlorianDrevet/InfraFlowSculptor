using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ApplicationStrategyRule : IValidationRule
{
    public string Code => "VAL-APP-STRATEGIE";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Snapshot.Applications
        .Where(application => !context.DeploymentStrategies.Supports(application.DeploymentStrategy))
        .Select(application => context.Finding(
            Code,
            Severity,
            new ObjectRef(ObjectType.Application, application.Id),
            $"La stratégie « {application.DeploymentStrategy} » de l’application « {application.Name} » n’est pas disponible.",
            $"Deployment strategy '{application.DeploymentStrategy}' for application '{application.Name}' is unavailable."))
        .ToArray();
}
