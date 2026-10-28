using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Commands.CreateProject;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(80);
        RuleFor(command => command.Code).NotEmpty().MaximumLength(10);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.CatalogVersion).NotEmpty().MaximumLength(64);
    }
}
