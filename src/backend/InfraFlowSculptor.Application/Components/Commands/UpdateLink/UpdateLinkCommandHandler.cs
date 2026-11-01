using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.UpdateLink;

public sealed class UpdateLinkCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider,
    LinkKindRegistry handlers,
    ModelSnapshotBuilder snapshotBuilder,
    ImpactAnalyzer impactAnalyzer)
    : ICommandHandler<UpdateLinkCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        UpdateLinkCommand command,
        CancellationToken cancellationToken)
    {
        var link = await dbContext.Query<LinkAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == command.LinkId
                && candidate.ProjectId == new ProjectId(command.ProjectId)
                && candidate.SourceComponentId == command.ComponentId
                && candidate.SourceResourceId == command.SourceResourceId,
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

        var validated = await LinkCommandSupport.LoadAndValidateAsync(
            dbContext, currentOrganization, catalogProvider, handlers,
            command.ProjectId, command.ComponentId, command.SourceResourceId,
            command.TargetComponentId, command.TargetResourceId, command.Kind,
            command.IdentityKind, command.IdentityResourceId, command.Roles,
            command.AccessScope, command.DataAccessLevel, link.Id, null, cancellationToken);
        if (validated.IsError)
        {
            return validated.Errors;
        }

        var normalizedRoles = LinkCommandSupport.NormalizeRoleNames(command.Roles);

        var previousTargetComponent = link.TargetComponentId == command.ComponentId
            || link.TargetComponentId == validated.Value.TargetComponent.Id.Value
                ? null
                : await dbContext.Query<InfraFlowSculptor.Domain.ComponentAggregate.ComponentAggregate>()
                    .SingleOrDefaultAsync(candidate => candidate.ProjectId == new ProjectId(command.ProjectId)
                        && candidate.Id == new ComponentId(link.TargetComponentId), cancellationToken);

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

        var replacement = new LinkSnapshot(
            link.Id,
            command.ComponentId,
            command.SourceResourceId,
            command.TargetComponentId,
            command.TargetResourceId,
            command.Kind,
            LinkOrigin.Explicit,
            command.IdentityKind,
            command.IdentityResourceId,
            normalizedRoles,
            command.AccessScope,
            command.DataAccessLevel);
        var impact = impactAnalyzer.AnalyzeLinkUpdate(built.Value, replacement, catalog);
        if (impact.Severity == ImpactSeverity.Blocker)
        {
            return Error.Validation("LINK_UPDATE_BLOCKED", "The link cannot be updated because the change would break a required link.");
        }

        var updated = link.Update(
            command.ComponentId,
            command.SourceResourceId,
            command.TargetComponentId,
            command.TargetResourceId,
            command.Kind,
            command.IdentityKind,
            command.IdentityResourceId,
            normalizedRoles,
            command.AccessScope,
            command.DataAccessLevel);
        if (updated.IsError)
        {
            return updated.Errors;
        }

        LinkCommandSupport.RecordComponentLinkChanges(
            validated.Value.SourceComponent,
            previousTargetComponent,
            validated.Value.TargetComponent);
        return new ModelCommandOutcome("Link updated.", impact.Items.Select(item => item.Message).ToArray());
    }
}
