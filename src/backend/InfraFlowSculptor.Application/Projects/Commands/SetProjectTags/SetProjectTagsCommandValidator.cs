using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Commands.SetProjectTags;

public sealed class SetProjectTagsCommandValidator : AbstractValidator<SetProjectTagsCommand>
{
    public SetProjectTagsCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Tags).NotNull();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
