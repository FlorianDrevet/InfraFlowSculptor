using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Queries.PreviewLink;

public sealed class PreviewLinkQueryHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    LinkKindRegistry handlers,
    LinkPreviewAnalyzer previewAnalyzer,
    TimeProvider clock)
    : IQueryHandler<PreviewLinkQuery, ErrorOr<LinkPreviewResult>>
{
    public async ValueTask<ErrorOr<LinkPreviewResult>> Handle(
        PreviewLinkQuery query,
        CancellationToken cancellationToken)
    {
        var validated = await LinkCommandSupport.LoadAndValidateAsync(
            dbContext,
            currentOrganization,
            catalogProvider,
            handlers,
            query.ProjectId,
            query.ComponentId,
            query.SourceResourceId,
            query.TargetComponentId,
            query.TargetResourceId,
            query.Kind,
            query.IdentityKind,
            query.IdentityResourceId,
            query.Roles,
            query.AccessScope,
            query.DataAccessLevel,
            updatingLinkId: null,
            expectedComponentVersion: null,
            cancellationToken);
        if (validated.IsError)
        {
            return validated.Errors;
        }

        var built = await snapshotBuilder.BuildAsync(query.ProjectId, cancellationToken);
        if (built.IsError)
        {
            return built.Errors;
        }

        var snapshot = built.Value;
        var catalog = catalogProvider.GetVersion(snapshot.Project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var candidate = new LinkSnapshot(
            Guid.CreateVersion7(clock.GetUtcNow()),
            query.ComponentId,
            query.SourceResourceId,
            query.TargetComponentId,
            query.TargetResourceId,
            query.Kind,
            LinkOrigin.Explicit,
            query.IdentityKind,
            query.IdentityResourceId,
            query.Roles,
            query.AccessScope,
            query.DataAccessLevel);

        return previewAnalyzer.Preview(snapshot, catalog, candidate);
    }
}
