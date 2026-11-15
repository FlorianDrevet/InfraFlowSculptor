using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Publication;

public sealed class SetProjectPublishPlanCommandValidator : AbstractValidator<SetProjectPublishPlanCommand>
{
    public SetProjectPublishPlanCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
        RuleFor(command => command.Preset).IsInEnum();
        RuleFor(command => command.Destinations).NotNull();
        RuleFor(command => command.Assignments).NotNull();
        RuleForEach(command => command.Destinations).ChildRules(destination =>
        {
            destination.RuleFor(value => value.Id).NotEmpty();
            destination.RuleFor(value => value.GitConnectionId).NotEmpty();
            destination.RuleFor(value => value.ProviderRepositoryId).NotEmpty().MaximumLength(300);
            destination.RuleFor(value => value.BasePath).MaximumLength(200);
        });
        RuleForEach(command => command.Assignments).ChildRules(assignment =>
        {
            assignment.RuleFor(value => value.ComponentId).NotEmpty();
            assignment.RuleFor(value => value.DestinationId).NotEmpty();
            assignment.RuleFor(value => value.Part).IsInEnum();
        });
    }
}
