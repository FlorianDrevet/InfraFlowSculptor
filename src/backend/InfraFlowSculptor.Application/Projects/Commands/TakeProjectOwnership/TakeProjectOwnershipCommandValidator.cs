using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Commands.TakeProjectOwnership;

public sealed class TakeProjectOwnershipCommandValidator : AbstractValidator<TakeProjectOwnershipCommand>
{
    public TakeProjectOwnershipCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
