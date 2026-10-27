using FluentValidation;

namespace InfraFlowSculptor.Application.Projects.Wizard;

public sealed class CreateProjectFromWizardCommandValidator : AbstractValidator<CreateProjectFromWizardCommand>
{
    public CreateProjectFromWizardCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MinimumLength(3).MaximumLength(80);
        RuleFor(command => command.Code).NotEmpty().MaximumLength(10);
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.CatalogVersion).NotEmpty().MaximumLength(64);
        RuleFor(command => command.NamingPreset).NotEmpty().MaximumLength(16);
        RuleFor(command => command.DefaultNameTemplate).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ResourceGroupNameTemplate).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Environments).NotEmpty();
        RuleForEach(command => command.Environments).ChildRules(environment =>
        {
            environment.RuleFor(item => item.Id).NotEmpty();
            environment.RuleFor(item => item.Name).NotEmpty().MaximumLength(50);
            environment.RuleFor(item => item.Code).NotEmpty().MaximumLength(8);
            environment.RuleFor(item => item.SubscriptionId).NotEmpty();
            environment.RuleFor(item => item.Region).NotEmpty().MaximumLength(64);
            environment.RuleFor(item => item.Approvers).NotNull();
            environment.RuleFor(item => item.Description).MaximumLength(500);
            environment.RuleFor(item => item.Approvers)
                .NotEmpty()
                .When(item => item.IsProtected);
        });
        RuleFor(command => command.Tags).NotNull();
    }
}
