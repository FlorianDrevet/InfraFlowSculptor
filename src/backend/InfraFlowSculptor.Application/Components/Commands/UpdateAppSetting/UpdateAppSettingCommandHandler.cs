using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands.CreateAppSetting;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Engine.Links;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.UpdateAppSetting;

public sealed class UpdateAppSettingCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider,
    ModelSnapshotBuilder snapshotBuilder,
    WiringEngine wiringEngine)
    : ICommandHandler<UpdateAppSettingCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(UpdateAppSettingCommand command, CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(dbContext, currentOrganization,
            command.ProjectId, command.ComponentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var setting = await dbContext.Query<AppSettingAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == command.AppSettingId
                && candidate.ProjectId == new ProjectId(command.ProjectId)
                && candidate.ComponentId == new ComponentId(command.ComponentId)
                && candidate.ApplicationResourceId == command.ApplicationResourceId,
                cancellationToken);
        if (setting is null)
        {
            return Error.NotFound("APP_SETTING_NOT_FOUND", "The application setting was not found.");
        }

        if (setting.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The application setting changed. Reload it before saving.",
                new Dictionary<string, object> { ["currentVersion"] = setting.Version });
        }

        var project = loaded.Value.Project;
        var component = loaded.Value.Component;
        var application = component.Resources.SingleOrDefault(resource => resource.Id == command.ApplicationResourceId);
        if (application is null)
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The application resource was not found.");
        }

        var catalog = catalogProvider.GetVersion(project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var projectComponents = await dbContext.Query<ComponentAggregate>()
            .Where(candidate => candidate.ProjectId == new ProjectId(command.ProjectId))
            .ToArrayAsync(cancellationToken);
        var resources = projectComponents.SelectMany(candidate => candidate.Resources
            .Select(resource => (Component: candidate, Resource: resource))).ToArray();
        var definition = AppSettingCommandSupport.ApplyPipelineVariableDefault(command.Definition, resources);
        var references = AppSettingCommandSupport.ValidateReferences(
            project, component, application, definition, resources, catalog);
        if (references.IsError)
        {
            return references.Errors;
        }

        var allSettings = await dbContext.Query<AppSettingAggregate>()
            .Where(candidate => candidate.ProjectId == new ProjectId(command.ProjectId))
            .ToArrayAsync(cancellationToken);
        var explicitNameConflict = await AppSettingCommandSupport.ValidateImplicitNameConflictAsync(
            snapshotBuilder,
            wiringEngine,
            catalog,
            new ProjectId(command.ProjectId),
            command.ApplicationResourceId,
            definition,
            cancellationToken);
        if (explicitNameConflict.IsError)
        {
            return explicitNameConflict.Errors;
        }

        var updated = setting.Update(definition, allSettings);
        if (updated.IsError)
        {
            return updated.Errors;
        }

        IReadOnlyList<string> findings = setting.SourceKind == AppSettingSourceKind.Literal
            && SecretValueDetector.HasSecretLikeName(setting.Name)
                ? [$"VAL-PAR-SECRET-SUSPECT: The literal setting name resembles a secret; convert it to a pipeline secret when appropriate."]
                : [];
        return new ModelCommandOutcome($"Application setting {setting.Name} updated.", findings);
    }
}
