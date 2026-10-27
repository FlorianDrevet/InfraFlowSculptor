using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Commands.DeleteProject;

public sealed class DeleteProjectCommandValidator : AbstractValidator<DeleteProjectCommand>
{
    public DeleteProjectCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.NameConfirmation).NotEmpty().MaximumLength(80);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
