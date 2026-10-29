using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Links;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceDeletionImpact;

public sealed class GetResourceDeletionImpactQueryHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    ImpactAnalyzer impactAnalyzer)
    : IQueryHandler<GetResourceDeletionImpactQuery, ErrorOr<ResourceDeletionImpactSummary>>
{
    public async ValueTask<ErrorOr<ResourceDeletionImpactSummary>> Handle(
        GetResourceDeletionImpactQuery query,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(
            dbContext,
            currentOrganization,
            query.ProjectId,
            query.ComponentId,
            cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var resource = loaded.Value.Component.Resources.SingleOrDefault(candidate => candidate.Id == query.ResourceId);
        if (resource is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The resource was not found.");
        }

        var built = await snapshotBuilder.BuildAsync(query.ProjectId, cancellationToken);
        if (built.IsError)
        {
            return built.Errors;
        }

        var catalog = catalogProvider.GetVersion(built.Value.Project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var linkImpact = impactAnalyzer.AnalyzeResourceDeletion(built.Value, resource.Id, catalog);
        return new ResourceDeletionImpactSummary(
            resource.Version,
            resource.Children
                .OrderBy(child => child.Type, StringComparer.Ordinal)
                .ThenBy(child => child.Name, StringComparer.Ordinal)
                .Select(child => new ResourceDeletionImpactItemSummary(child.Type, child.Name))
                .ToArray(),
            linkImpact);
    }
}
