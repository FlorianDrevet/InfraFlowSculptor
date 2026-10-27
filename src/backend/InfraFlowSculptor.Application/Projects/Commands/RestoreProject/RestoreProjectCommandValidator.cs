using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Commands.RestoreProject;

public sealed class RestoreProjectCommandValidator : AbstractValidator<RestoreProjectCommand>
{
    public RestoreProjectCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
