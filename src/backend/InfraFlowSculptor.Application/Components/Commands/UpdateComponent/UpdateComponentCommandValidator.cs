using FluentValidation;
using InfraFlowSculptor.Domain.ComponentAggregate;

namespace InfraFlowSculptor.Application.Components.Commands.UpdateComponent;

public sealed class UpdateComponentCommandValidator : AbstractValidator<UpdateComponentCommand>
{
    public UpdateComponentCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ComponentId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(60);
        RuleFor(command => command.Code).NotEmpty().MaximumLength(20);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.Mode).IsInEnum();
        RuleFor(command => command.TargetEnvironmentIds).NotNull();
        RuleFor(command => command.RemovedResourceBehavior).IsInEnum();
        RuleFor(command => command.Tags).NotNull();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
        When(command => command.Mode == ComponentDeploymentMode.Single, () =>
        {
            RuleFor(command => command.SingleTargetSubscriptionId).NotEmpty();
            RuleFor(command => command.SingleTargetRegion).NotEmpty().MaximumLength(64);
        });
    }
}
