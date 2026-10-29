using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Mediator;
using Microsoft.EntityFrameworkCore;
using InfraFlowSculptor.Engine.Links;

namespace InfraFlowSculptor.Application.Components.Queries.GetAppSettings;

public sealed class GetAppSettingsQueryHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    WiringEngine wiringEngine)
    : IQueryHandler<GetAppSettingsQuery, ErrorOr<IReadOnlyList<AppSettingSummary>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<AppSettingSummary>>> Handle(
        GetAppSettingsQuery query,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(dbContext, currentOrganization,
            query.ProjectId, query.ComponentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        if (!loaded.Value.Component.Resources.Any(resource => resource.Id == query.ApplicationResourceId))
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The application resource was not found.");
        }

        var settings = await dbContext.Query<AppSettingAggregate>()
            .Where(setting => setting.ProjectId == new ProjectId(query.ProjectId)
                && setting.ComponentId == new ComponentId(query.ComponentId)
                && setting.ApplicationResourceId == query.ApplicationResourceId)
            .OrderBy(setting => setting.NameKey)
            .ToArrayAsync(cancellationToken);

        var explicitSettings = settings.Select(setting => new AppSettingSummary(
            setting.Id,
            setting.Version,
            setting.Name,
            setting.SourceKind,
            setting.LiteralSource?.Value,
            setting.LiteralSource?.Overrides.ToDictionary(value => value.TargetId, value => value.Value),
            setting.SourceResourceId,
            setting.OutputName,
            setting.KeyVaultResourceId,
            setting.SecretName,
            setting.SecretFeedKind,
            setting.PipelineVariableName,
            setting.IdentityResourceId,
            setting.SecretValueDeclaredNonSecret,
            setting.SourceKind == AppSettingSourceKind.Literal && SecretValueDetector.HasSecretLikeName(setting.Name)
                ? ["VAL-PAR-SECRET-SUSPECT: The literal setting name resembles a secret; convert it to a pipeline secret when appropriate."]
                : [],
            IsImplicit: false,
            Origin: null)).ToArray();

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

        var implicitSettings = wiringEngine.Evaluate(built.Value, catalog).ImplicitApplicationSettings
            .Where(setting => setting.ApplicationResourceId == query.ApplicationResourceId)
            .Select(setting => new AppSettingSummary(
                setting.Id,
                1,
                setting.Name,
                setting.OutputName is null
                    ? AppSettingSourceKind.Literal
                    : AppSettingSourceKind.ResourceOutput,
                setting.Value,
                new Dictionary<Guid, string>(),
                setting.SourceResourceId,
                setting.OutputName,
                null,
                null,
                null,
                null,
                null,
                false,
                [],
                IsImplicit: true,
                Origin: setting.Origin))
            .ToArray();

        return explicitSettings.Concat(implicitSettings)
            .OrderBy(setting => setting.IsImplicit)
            .ThenBy(setting => setting.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
