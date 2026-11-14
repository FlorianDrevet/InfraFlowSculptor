using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.RevisionAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Generation;

public sealed class ListProjectRevisionsQueryHandler(IIfsDbContext dbContext)
    : IQueryHandler<ListProjectRevisionsQuery, ErrorOr<RevisionListView>>
{
    public async ValueTask<ErrorOr<RevisionListView>> Handle(
        ListProjectRevisionsQuery query,
        CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(query.ProjectId);
        var project = await dbContext.Query<ProjectAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Error.NotFound("PROJECT_NOT_FOUND", "The project was not found.");
        }

        var revisions = await dbContext.Query<RevisionAggregate>()
            .Where(revision => revision.ProjectId == projectId)
            .OrderByDescending(revision => revision.Number)
            .ToArrayAsync(cancellationToken);
        var queuedPositions = await dbContext.Query<RevisionAggregate>()
            .Where(revision => revision.OrganizationId == project.OrganizationId
                && revision.State == RevisionState.Queued)
            .OrderBy(revision => revision.CreatedAt)
            .ThenBy(revision => revision.Number)
            .Select(revision => revision.Id)
            .ToArrayAsync(cancellationToken);
        var queuePositionById = queuedPositions
            .Select((revisionId, index) => (RevisionId: revisionId, Position: index + 1))
            .ToDictionary(entry => entry.RevisionId, entry => entry.Position);

        var items = revisions.Select(revision => new RevisionView(
                revision,
                IsStale(revision, project),
                revision.State == RevisionState.Queued
                    ? GetQueuePosition(revision.Id, queuePositionById)
                    : null))
            .ToArray();

        return new RevisionListView(items, revisions.FirstOrDefault()?.Number + 1 ?? 1);
    }

    private static bool IsStale(RevisionAggregate revision, ProjectAggregate project) =>
        revision.ModelVersion != project.ModelVersion
        || !string.Equals(revision.CatalogVersion, project.CatalogVersion, StringComparison.Ordinal)
        || !string.Equals(revision.InfrastructureLanguage, project.InfrastructureLanguage, StringComparison.Ordinal)
        || !string.Equals(revision.CiPlatform, project.CiPlatform, StringComparison.Ordinal);

    internal static int? GetQueuePosition(RevisionId revisionId, IReadOnlyDictionary<RevisionId, int> queuePositionById) =>
        queuePositionById.TryGetValue(revisionId, out var position) ? position : null;
}
