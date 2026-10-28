using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Commands.UpdateProjectEnvironment;

public sealed class UpdateProjectEnvironmentCommandValidator : AbstractValidator<UpdateProjectEnvironmentCommand>
{
    public UpdateProjectEnvironmentCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.EnvironmentId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Code).NotEmpty().MaximumLength(8);
        RuleFor(command => command.SubscriptionId).NotEmpty();
        RuleFor(command => command.Region).NotEmpty().MaximumLength(64);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.Tags).NotNull();
        RuleFor(command => command.Approvers).NotNull();
        RuleFor(command => command.Runners).NotNull();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
