using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Commands.MoveProjectEnvironment;

public sealed class MoveProjectEnvironmentCommandValidator : AbstractValidator<MoveProjectEnvironmentCommand>
{
    public MoveProjectEnvironmentCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.EnvironmentId).NotEmpty();
        RuleFor(command => command.Position).GreaterThan(0);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
