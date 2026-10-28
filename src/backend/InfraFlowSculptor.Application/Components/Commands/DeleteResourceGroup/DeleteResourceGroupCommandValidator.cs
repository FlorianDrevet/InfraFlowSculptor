using FluentValidation;

namespace InfraFlowSculptor.Application.Components.Commands.DeleteResourceGroup;

public sealed class DeleteResourceGroupCommandValidator : AbstractValidator<DeleteResourceGroupCommand>
{
    public DeleteResourceGroupCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ComponentId).NotEmpty();
        RuleFor(command => command.ResourceGroupId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
