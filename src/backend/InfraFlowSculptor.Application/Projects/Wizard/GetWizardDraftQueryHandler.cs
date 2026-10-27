using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Wizard;

public sealed class GetWizardDraftQueryHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICurrentUser currentUser,
    TimeProvider clock)
    : IQueryHandler<GetWizardDraftQuery, ErrorOr<ProjectWizardDraftView?>>
{
    public async ValueTask<ErrorOr<ProjectWizardDraftView?>> Handle(
        GetWizardDraftQuery query,
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
            return (ProjectWizardDraftView?)null;
        }

        var draft = await dbContext.Query<ProjectWizardDraft>()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId
                && candidate.UserProfileId == userProfileId, cancellationToken);
        return draft is null || draft.IsExpired(clock.GetUtcNow())
            ? (ProjectWizardDraftView?)null
            : new ProjectWizardDraftView(draft.ContentJson, draft.ExpiresAt);
    }
}
