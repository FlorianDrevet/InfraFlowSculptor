using FluentValidation;

namespace InfraFlowSculptor.Application.Components.Commands.DeleteComponent;

public sealed class DeleteComponentCommandValidator : AbstractValidator<DeleteComponentCommand>
{
    public DeleteComponentCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ComponentId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
