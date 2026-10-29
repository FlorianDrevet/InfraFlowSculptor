using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.RemoveLink;

public sealed class RemoveLinkCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    ImpactAnalyzer impactAnalyzer)
    : ICommandHandler<RemoveLinkCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        RemoveLinkCommand command,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(
            dbContext, currentOrganization, command.ProjectId, command.ComponentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var link = await dbContext.Query<LinkAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == command.LinkId
                && candidate.ProjectId == new ProjectId(command.ProjectId)
                && candidate.SourceComponentId == command.ComponentId
                && candidate.SourceResourceId == command.ResourceId,
                cancellationToken);
        if (link is null || currentOrganization.Id != link.OrganizationId)
        {
            return Error.NotFound("LINK_NOT_FOUND", "The link was not found.");
        }

        var version = LinkCommandSupport.CheckLinkVersion(link, command.ExpectedLinkVersion);
        if (version.IsError)
        {
            return version.Errors;
        }

        var built = await snapshotBuilder.BuildAsync(command.ProjectId, cancellationToken);
        if (built.IsError)
        {
            return built.Errors;
        }

        var catalog = catalogProvider.GetVersion(built.Value.Project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var impact = impactAnalyzer.AnalyzeLinkRemoval(built.Value, link.Id, catalog);
        if (impact.Severity == ImpactSeverity.Blocker)
        {
            return Error.Validation("LINK_REMOVE_BLOCKED", "The link cannot be removed because it is required by the source resource.");
        }

        var targetComponent = link.TargetComponentId == command.ComponentId
            ? null
            : await dbContext.Query<ComponentAggregate>()
                .SingleOrDefaultAsync(candidate => candidate.ProjectId == new ProjectId(command.ProjectId)
                    && candidate.Id == new ComponentId(link.TargetComponentId), cancellationToken);

        dbContext.Query<LinkAggregate>().Remove(link);
        LinkCommandSupport.RecordComponentLinkChanges(loaded.Value.Component, targetComponent);
        return new ModelCommandOutcome("Link removed.", impact.Items.Select(item => item.Message).ToArray());
    }
}
