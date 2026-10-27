using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Wizard;

public sealed class DiscardWizardDraftCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICurrentUser currentUser)
    : ICommandHandler<DiscardWizardDraftCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(
        DiscardWizardDraftCommand command,
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
            return Result.Success;
        }

        var draft = await dbContext.Query<ProjectWizardDraft>()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId
                && candidate.UserProfileId == userProfileId, cancellationToken);
        if (draft is not null)
        {
            dbContext.Query<ProjectWizardDraft>().Remove(draft);
        }

        return Result.Success;
    }
}
