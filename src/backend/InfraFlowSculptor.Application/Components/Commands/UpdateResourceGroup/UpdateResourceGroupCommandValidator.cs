using FluentValidation;

namespace InfraFlowSculptor.Application.Components.Commands.UpdateResourceGroup;

public sealed class UpdateResourceGroupCommandValidator : AbstractValidator<UpdateResourceGroupCommand>
{
    public UpdateResourceGroupCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ComponentId).NotEmpty();
        RuleFor(command => command.ResourceGroupId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(40);
        RuleFor(command => command.Region).MaximumLength(64);
        RuleFor(command => command.Tags).NotNull();
        RuleFor(command => command.ForcedNames).NotNull();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
