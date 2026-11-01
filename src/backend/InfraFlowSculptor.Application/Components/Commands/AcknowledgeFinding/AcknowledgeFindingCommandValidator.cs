using FluentValidation;
using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Application.Components.Commands.AcknowledgeFinding;

public sealed class AcknowledgeFindingCommandValidator : AbstractValidator<AcknowledgeFindingCommand>
{
    public AcknowledgeFindingCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.FindingCode)
            .NotEmpty()
            .MaximumLength(64)
            .Matches("^VAL-[A-Z0-9]+(?:-[A-Z0-9]+)*$");
        RuleFor(command => command.SubjectId).NotEmpty();
        RuleFor(command => command.SubjectType).IsInEnum();
        RuleFor(command => command.EnvironmentId).NotEqual(Guid.Empty);
        RuleFor(command => command.ExpectedObjectVersion).GreaterThan(0);
        RuleFor(command => command.Comment).NotEmpty().MaximumLength(2000);
        RuleFor(command => command.DetailKey)
            .MaximumLength(128)
            .Must(value => value is null || !string.IsNullOrWhiteSpace(value));
        RuleFor(command => command.SubjectType)
            .Must(type => type != ObjectType.FindingAcknowledgment)
            .WithMessage("A finding acknowledgment cannot acknowledge itself.");
    }
}
