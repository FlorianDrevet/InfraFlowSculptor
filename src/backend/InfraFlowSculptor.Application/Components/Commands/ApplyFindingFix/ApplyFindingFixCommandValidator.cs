using FluentValidation;

namespace InfraFlowSculptor.Application.Components.Commands.ApplyFindingFix;

public sealed class ApplyFindingFixCommandValidator : AbstractValidator<ApplyFindingFixCommand>
{
    public ApplyFindingFixCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.FindingCode)
            .NotEmpty()
            .MaximumLength(64)
            .Matches("^VAL-[A-Z0-9]+(?:-[A-Z0-9]+)*$");
        RuleFor(command => command.SubjectType).IsInEnum();
        RuleFor(command => command.SubjectType).NotEqual(InfraFlowSculptor.Domain.Common.ObjectType.FindingAcknowledgment);
        RuleFor(command => command.SubjectId).NotEmpty();
        RuleFor(command => command.EnvironmentId).NotEqual(Guid.Empty);
        RuleFor(command => command.ExpectedObjectVersion).GreaterThan(0);
        RuleFor(command => command.DetailKey)
            .MaximumLength(128)
            .Must(value => value is null || !string.IsNullOrWhiteSpace(value));
    }
}
