using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.SetProjectFavorite;

public sealed class SetProjectFavoriteCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser)
    : ICommandHandler<SetProjectFavoriteCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(
        SetProjectFavoriteCommand command,
        CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(command.ProjectId);
        var projectExists = await dbContext.Query<ProjectAggregate>()
            .AnyAsync(project => project.Id == projectId, cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var profile = await dbContext.Query<UserProfile>()
            .Include(candidate => candidate.FavoriteProjects)
            .SingleOrDefaultAsync(candidate => candidate.TenantId == currentUser.Key.TenantId
                && candidate.ObjectId == currentUser.Key.ObjectId,
                cancellationToken);
        if (profile is null)
        {
            return Error.NotFound("NOT_FOUND", "The current user profile was not found.");
        }

        var updated = profile.SetProjectFavorite(projectId, command.IsFavorite);
        if (updated.IsError)
        {
            return updated.Errors;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
