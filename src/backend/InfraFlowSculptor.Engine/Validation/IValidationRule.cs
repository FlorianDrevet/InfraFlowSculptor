namespace InfraFlowSculptor.Engine.Validation;

public interface IValidationRule
{
    string Code { get; }

    FindingSeverity Severity { get; }

    string Lot { get; }

    IReadOnlyList<ValidationFinding> Evaluate(ValidationContext context);
}
