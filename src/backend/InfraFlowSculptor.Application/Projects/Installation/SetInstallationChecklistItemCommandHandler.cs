using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Installation;

public sealed class SetInstallationChecklistItemCommandHandler(
    IMediator mediator,
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICurrentUser currentUser,
    TimeProvider clock)
    : ICommandHandler<SetInstallationChecklistItemCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(
        SetInstallationChecklistItemCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ItemKey) || command.ItemKey.Length > 160)
        {
            return Error.Validation("INSTALLATION_CHECKLIST_ITEM_INVALID", "The installation checklist item key is invalid.");
        }

        var checklistResult = await mediator.Send(
            new GetInstallationChecklistQuery(command.ProjectId),
            cancellationToken);
        if (checklistResult.IsError)
        {
            return checklistResult.Errors;
        }

        var item = checklistResult.Value.Sections
            .SelectMany(section => section.Items)
            .SingleOrDefault(candidate => string.Equals(candidate.Key, command.ItemKey, StringComparison.Ordinal));
        if (item is null)
        {
            return Error.NotFound("INSTALLATION_CHECKLIST_ITEM_NOT_FOUND", "The installation checklist item was not found for the current revision.");
        }

        if (!item.IsUserActionable || item.State == InstallationChecklistItemState.Automatic)
        {
            return Error.Validation("INSTALLATION_CHECKLIST_ITEM_NOT_ACTIONABLE", "This installation checklist item is completed automatically and cannot be changed manually.");
        }

        var projectId = new ProjectId(command.ProjectId);
        var progress = await dbContext.Query<InstallationChecklistProgressAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.ProjectId == projectId
                && candidate.ItemKey == command.ItemKey,
                cancellationToken);
        if ((progress?.CompletedAt is not null) == command.IsCompleted)
        {
            return Result.Success;
        }

        if (!command.IsCompleted && progress is null)
        {
            return Result.Success;
        }

        var profile = await dbContext.Query<UserProfile>()
            .SingleOrDefaultAsync(candidate => candidate.TenantId == currentUser.Key.TenantId
                && candidate.ObjectId == currentUser.Key.ObjectId,
                cancellationToken);
        if (profile is null)
        {
            return Error.NotFound("USER_PROFILE_NOT_FOUND", "The current user profile was not found.");
        }

        var now = clock.GetUtcNow();
        if (progress is null)
        {
            if (currentOrganization.Id is not { } organizationId)
            {
                return Error.NotFound("NOT_FOUND", "The project was not found.");
            }

            progress = InstallationChecklistProgressAggregate.Complete(
                Guid.CreateVersion7(now),
                projectId,
                organizationId,
                command.ItemKey,
                profile.Id,
                now);
            dbContext.Query<InstallationChecklistProgressAggregate>().Add(progress);
        }
        else
        {
            progress.SetCompleted(command.IsCompleted, profile.Id, now);
        }

        return Result.Success;
    }
}
