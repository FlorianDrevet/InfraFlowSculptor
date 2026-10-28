using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Users.Commands.UpdateUserPreferences;

public sealed class UpdateUserPreferencesCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateUserPreferencesCommand, ErrorOr<UserPreferences>>
{
    public async ValueTask<ErrorOr<UserPreferences>> Handle(
        UpdateUserPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.Query<UserProfile>()
            .SingleOrDefaultAsync(
                candidate => candidate.TenantId == currentUser.Key.TenantId
                    && candidate.ObjectId == currentUser.Key.ObjectId,
                cancellationToken);

        if (profile is null)
        {
            return Error.NotFound("USER_PROFILE_NOT_FOUND", "The authenticated user profile was not found.");
        }

        var updated = profile.SetPreferences(command.PreferredLanguage, command.PreferredTheme);
        if (updated.IsError)
        {
            return updated.Errors;
        }

        return new UserPreferences(profile.PreferredLanguage, profile.PreferredTheme);
    }
}
