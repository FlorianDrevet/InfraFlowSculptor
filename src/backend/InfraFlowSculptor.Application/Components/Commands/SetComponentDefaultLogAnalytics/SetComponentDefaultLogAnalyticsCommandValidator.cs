using FluentValidation;

namespace InfraFlowSculptor.Application.Components.Commands.SetComponentDefaultLogAnalytics;

public sealed class SetComponentDefaultLogAnalyticsCommandValidator : AbstractValidator<SetComponentDefaultLogAnalyticsCommand>
{
    public SetComponentDefaultLogAnalyticsCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ComponentId).NotEmpty();
        RuleFor(command => command.WorkspaceResourceId)
            .Must(resourceId => resourceId != Guid.Empty)
            .WithMessage("L’espace Log Analytics doit être une ressource valide.");
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
