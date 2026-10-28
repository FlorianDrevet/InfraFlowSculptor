using FluentValidation;

namespace InfraFlowSculptor.Application.Users.Commands.UpdateUserPreferences;

public sealed class UpdateUserPreferencesCommandValidator : AbstractValidator<UpdateUserPreferencesCommand>
{
    public UpdateUserPreferencesCommandValidator()
    {
        RuleFor(command => command.PreferredLanguage)
            .Must(language => language is "fr" or "en")
            .WithErrorCode("USER_LANGUAGE_INVALID");
        RuleFor(command => command.PreferredTheme)
            .Must(theme => theme is "system" or "dark" or "light")
            .WithErrorCode("USER_THEME_INVALID");
    }
}
