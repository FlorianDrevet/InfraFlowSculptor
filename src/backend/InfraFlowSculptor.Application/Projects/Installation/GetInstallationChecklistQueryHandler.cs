using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Components.Queries.GetDeploymentPlan;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.RevisionAggregate;
using InfraFlowSculptor.Engine.Plan;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Installation;

public sealed class GetInstallationChecklistQueryHandler(
    IMediator mediator,
    IIfsDbContext dbContext,
    TimeProvider clock)
    : IQueryHandler<GetInstallationChecklistQuery, ErrorOr<InstallationChecklistView>>
{
    public async ValueTask<ErrorOr<InstallationChecklistView>> Handle(
        GetInstallationChecklistQuery query,
        CancellationToken cancellationToken)
    {
        var livePlanResult = await mediator.Send(
            new GetDeploymentPlanQuery(query.ProjectId, null, null),
            cancellationToken);
        if (livePlanResult.IsError)
        {
            return livePlanResult.Errors;
        }

        var projectId = new ProjectId(query.ProjectId);
        var revisions = await dbContext.Query<RevisionAggregate>()
            .Where(revision => revision.ProjectId == projectId && revision.State == RevisionState.Generated)
            .OrderByDescending(revision => revision.Number)
            .Take(2)
            .ToArrayAsync(cancellationToken);

        var currentPlan = livePlanResult.Value;
        DeploymentPlan? previousPlan = null;
        int? revisionNumber = null;
        if (revisions.Length > 0)
        {
            currentPlan = DeploymentPlanJsonSerializer.Deserialize(revisions[0].DeploymentPlanJson);
            revisionNumber = revisions[0].Number;
            if (revisions.Length > 1)
            {
                previousPlan = DeploymentPlanJsonSerializer.Deserialize(revisions[1].DeploymentPlanJson);
            }
        }

        var checklist = InstallationChecklistBuilder.Build(currentPlan, previousPlan);
        var progress = await dbContext.Query<InstallationChecklistProgressAggregate>()
            .Where(item => item.ProjectId == projectId)
            .ToDictionaryAsync(item => item.ItemKey, StringComparer.Ordinal, cancellationToken);
        var now = clock.GetUtcNow();
        var sections = checklist.Sections.Select(section => new InstallationChecklistSectionView(
            section.Code,
            section.Title,
            section.Subtitle,
            section.Items.Select(item =>
            {
                progress.TryGetValue(item.Key, out var savedProgress);
                var completedAt = savedProgress?.CompletedAt;
                var state = InstallationChecklistStatusResolver.Resolve(
                    item.State,
                    completedAt is not null,
                    verifiedAt: null,
                    now);
                return new InstallationChecklistItemView(
                    item.Key,
                    item.Text,
                    state,
                    item.IsUserActionable,
                    item.Command,
                    item.IsNewSincePrevious,
                    completedAt);
            }).ToArray())).ToArray();

        return new InstallationChecklistView(revisionNumber, sections);
    }
}
