using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Commands.DeleteProjectEnvironment;

public sealed class DeleteProjectEnvironmentCommandValidator : AbstractValidator<DeleteProjectEnvironmentCommand>
{
    public DeleteProjectEnvironmentCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.EnvironmentId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
