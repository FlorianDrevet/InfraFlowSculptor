using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Wizard;

public sealed class SaveWizardDraftCommandValidator : AbstractValidator<SaveWizardDraftCommand>
{
    public SaveWizardDraftCommandValidator() => RuleFor(command => command.ContentJson).NotEmpty();
}
