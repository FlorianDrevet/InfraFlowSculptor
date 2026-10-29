using FluentValidation;
using InfraFlowSculptor.Engine.Naming;

namespace InfraFlowSculptor.Application.Components.Commands.SetComponentNaming;

public sealed class SetComponentNamingCommandValidator : AbstractValidator<SetComponentNamingCommand>
{
    public SetComponentNamingCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ComponentId).NotEmpty();
        RuleFor(command => command.DefaultTemplate)
            .Must(template => template is null || NamingEngine.ValidateTemplate(template).Count == 0)
            .WithMessage("Le gabarit par défaut du composant est invalide.");
        RuleFor(command => command.TypeOverrides).NotNull();
        RuleFor(command => command.TypeOverrides)
            .Must(overrides => overrides.Select(value => value.Type?.Trim() ?? string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() == overrides.Count)
            .When(command => command.TypeOverrides is not null)
            .WithMessage("Un seul réglage de nommage est autorisé par type.");
        RuleForEach(command => command.TypeOverrides)
            .Must(value => !string.IsNullOrWhiteSpace(value.Type))
            .WithMessage("Le type du catalogue est obligatoire.");
        RuleForEach(command => command.TypeOverrides)
            .Must(value => value.Template is null || NamingEngine.ValidateTemplate(value.Template).Count == 0)
            .WithMessage("Un gabarit par type est invalide.");
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
