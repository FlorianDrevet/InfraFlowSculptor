using FluentValidation;

namespace InfraFlowSculptor.Application.Components.Commands.CreateResourceGroup;

public sealed class CreateResourceGroupCommandValidator : AbstractValidator<CreateResourceGroupCommand>
{
    public CreateResourceGroupCommandValidator()
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
