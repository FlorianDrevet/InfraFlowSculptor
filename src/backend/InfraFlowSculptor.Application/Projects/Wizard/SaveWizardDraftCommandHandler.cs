using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Wizard;

public sealed class SaveWizardDraftCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICurrentUser currentUser,
    TimeProvider clock)
    : ICommandHandler<SaveWizardDraftCommand, ErrorOr<ProjectWizardDraftView>>
{
    public async ValueTask<ErrorOr<ProjectWizardDraftView>> Handle(
        SaveWizardDraftCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var userProfileId = await dbContext.Query<UserProfile>()
            .Where(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId)
            .Select(profile => profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (userProfileId.Value == Guid.Empty)
        {
            return Error.NotFound("NOT_FOUND", "The current user profile was not found.");
        }

        var now = clock.GetUtcNow();
        var draft = await dbContext.Query<ProjectWizardDraft>()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId
                && candidate.UserProfileId == userProfileId, cancellationToken);
        if (draft is null)
        {
            var created = ProjectWizardDraft.Create(
                organizationId,
                userProfileId,
                command.ContentJson,
                now);
            if (created.IsError)
            {
                return created.Errors;
            }

            draft = created.Value;
            dbContext.Query<ProjectWizardDraft>().Add(draft);
        }
        else
        {
            var saved = draft.Save(command.ContentJson, now);
            if (saved.IsError)
            {
                return saved.Errors;
            }
        }

        return new ProjectWizardDraftView(draft.ContentJson, draft.ExpiresAt);
    }
}
