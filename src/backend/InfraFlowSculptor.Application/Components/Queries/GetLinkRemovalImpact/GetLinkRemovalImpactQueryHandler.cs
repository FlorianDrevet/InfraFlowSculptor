using ErrorOr;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Links;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetLinkRemovalImpact;

public sealed class GetLinkRemovalImpactQueryHandler(
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    ImpactAnalyzer impactAnalyzer)
    : IQueryHandler<GetLinkRemovalImpactQuery, ErrorOr<LinkImpactSummary>>
{
    public async ValueTask<ErrorOr<LinkImpactSummary>> Handle(
        GetLinkRemovalImpactQuery query,
        CancellationToken cancellationToken)
    {
        var built = await snapshotBuilder.BuildAsync(query.ProjectId, cancellationToken);
        if (built.IsError)
        {
            return built.Errors;
        }

        var snapshot = built.Value;
        var link = snapshot.Links.SingleOrDefault(candidate => candidate.Id == query.LinkId
            && candidate.SourceComponentId == query.ComponentId
            && candidate.SourceResourceId == query.ResourceId);
        if (link is null)
        {
            return Error.NotFound("LINK_NOT_FOUND", "The link was not found.");
        }

        var catalog = catalogProvider.GetVersion(snapshot.Project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var result = impactAnalyzer.AnalyzeLinkRemoval(snapshot, link.Id, catalog);
        return new LinkImpactSummary(result.Severity, result.Items);
    }
}
