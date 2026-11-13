using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.RevisionAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Generation;

public sealed class GetProjectRevisionQueryHandler(IIfsDbContext dbContext)
    : IQueryHandler<GetProjectRevisionQuery, ErrorOr<RevisionView>>
{
    public async ValueTask<ErrorOr<RevisionView>> Handle(
        GetProjectRevisionQuery query,
        CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(query.ProjectId);
        var project = await dbContext.Query<ProjectAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Error.NotFound("PROJECT_NOT_FOUND", "The project was not found.");
        }

        var revision = await dbContext.Query<RevisionAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == new RevisionId(query.RevisionId)
                && candidate.ProjectId == projectId,
                cancellationToken);
        if (revision is null)
        {
            return Error.NotFound("REVISION_NOT_FOUND", "The revision was not found.");
        }

        int? queuePosition = null;
        if (revision.State == RevisionState.Queued)
        {
            queuePosition = await dbContext.Query<RevisionAggregate>()
                .CountAsync(candidate => candidate.OrganizationId == revision.OrganizationId
                    && candidate.State == RevisionState.Queued
                    && (candidate.CreatedAt < revision.CreatedAt
                        || (candidate.CreatedAt == revision.CreatedAt && candidate.Number <= revision.Number)),
                    cancellationToken);
        }

        var isStale = revision.ModelVersion != project.ModelVersion
            || !string.Equals(revision.CatalogVersion, project.CatalogVersion, StringComparison.Ordinal)
            || !string.Equals(revision.InfrastructureLanguage, project.InfrastructureLanguage, StringComparison.Ordinal)
            || !string.Equals(revision.CiPlatform, project.CiPlatform, StringComparison.Ordinal);
        return new RevisionView(revision, isStale, queuePosition);
    }
}
