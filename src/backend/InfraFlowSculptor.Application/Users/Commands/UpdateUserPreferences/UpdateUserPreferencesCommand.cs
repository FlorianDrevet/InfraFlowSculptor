using ErrorOr;
using Mediator;

namespace InfraFlowSculptor.Application.Users.Commands.UpdateUserPreferences;

public sealed record UpdateUserPreferencesCommand(string PreferredLanguage, string PreferredTheme)
    : ICommand<ErrorOr<UserPreferences>>;
