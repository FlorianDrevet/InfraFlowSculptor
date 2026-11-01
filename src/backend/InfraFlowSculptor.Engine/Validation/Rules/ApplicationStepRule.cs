using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Engine.Applications;

namespace InfraFlowSculptor.Engine.Validation.Rules;

public sealed class ApplicationStepRule : IValidationRule
{
    public string Code => "VAL-APP-ETAPE";

    public FindingSeverity Severity => FindingSeverity.Error;

    public string Lot => "lot1";

    public IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context) => context.Snapshot.Applications
        .SelectMany(application => ApplicationStepValidator.Validate(application.Steps, context.Catalog.PipelineSteps)
            .Select(issue => context.Finding(
                Code,
                Severity,
                new ObjectRef(ObjectType.Application, application.Id),
                issue.Message,
                $"Application step '{issue.StepName}' has an invalid or missing configuration.")))
        .ToArray();
}
