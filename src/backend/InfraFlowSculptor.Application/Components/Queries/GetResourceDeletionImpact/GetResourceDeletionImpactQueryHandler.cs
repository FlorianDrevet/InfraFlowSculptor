using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Engine.Links;
using Mediator;
using Microsoft.EntityFrameworkCore;

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
        var applicationSettings = await dbContext.Query<AppSettingAggregate>()
            .Where(setting => setting.ProjectId == new ProjectId(query.ProjectId)
                && (setting.ApplicationResourceId == resource.Id
                    || setting.SourceResourceId == resource.Id
                    || setting.KeyVaultResourceId == resource.Id
                    || setting.IdentityResourceId == resource.Id))
            .ToArrayAsync(cancellationToken);
        var settingImpacts = applicationSettings
            .OrderBy(setting => setting.NameKey)
            .Select(setting =>
            {
                var ownsApplication = setting.ApplicationResourceId == resource.Id;
                var referenceKind = setting.SourceResourceId == resource.Id
                    ? "resource output"
                    : setting.KeyVaultResourceId == resource.Id
                        ? "Key Vault"
                        : setting.IdentityResourceId == resource.Id
                            ? "identity"
                            : "application";
                var blocksDeletion = !ownsApplication;
                return new ResourceDeletionImpactItemSummary(
                    "Application setting",
                    setting.Name,
                    blocksDeletion ? "APP_SETTING_RESOURCE_IN_USE" : "APP_SETTING_DELETED_WITH_APPLICATION",
                    blocksDeletion
                        ? $"The setting '{setting.Name}' references this {referenceKind}. Update or delete the setting before deleting this resource."
                        : $"The setting '{setting.Name}' will be deleted with its application resource.",
                    blocksDeletion);
            })
            .Concat(built.Value.Components
                .SelectMany(component => component.Resources)
                .Where(application => application.DefaultIdentityResourceId == resource.Id)
                .Select(application => new ResourceDeletionImpactItemSummary(
                    "Application setting",
                    ApplicationSettingNameRules.AzureClientId,
                    "APP_SETTING_RESOURCE_IN_USE",
                    $"The application '{application.LogicalName}' uses this resource as its default identity. Change the default identity before deleting it.",
                    true)))
            .ToArray();
        return new ResourceDeletionImpactSummary(
            resource.Version,
            resource.Children
                .OrderBy(child => child.Type, StringComparer.Ordinal)
                .ThenBy(child => child.Name, StringComparer.Ordinal)
                .Select(child => new ResourceDeletionImpactItemSummary(
                    child.Type, child.Name, "RESOURCE_CHILD_DELETED", "The child resource will be deleted with its parent resource.", false))
                .Concat(settingImpacts)
                .ToArray(),
            linkImpact,
            settingImpacts.Any(item => item.BlocksDeletion));
    }
}
