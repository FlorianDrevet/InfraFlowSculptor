using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Applications;
using InfraFlowSculptor.Engine.Links;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.SaveApplication;

public sealed class SaveApplicationCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider,
    ModelSnapshotBuilder snapshotBuilder,
    LinkKindRegistry linkKinds)
    : ICommandHandler<SaveApplicationCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        SaveApplicationCommand command,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(
            dbContext, currentOrganization, command.ProjectId, command.ComponentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var (project, component) = loaded.Value;
        var resource = component.Resources.SingleOrDefault(candidate => candidate.Id == command.ResourceId);
        if (resource is null || !string.Equals(resource.Type, "ContainerApp", StringComparison.Ordinal))
        {
            return Error.NotFound("RESOURCE_NOT_FOUND", "The container application resource was not found.");
        }

        var catalog = catalogProvider.GetVersion(project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        const string buildProfileName = "Container";
        var profile = catalog.BuildProfiles.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, buildProfileName, StringComparison.Ordinal));
        if (profile is null || !profile.ResourceTypes.Contains(resource.Type, StringComparer.Ordinal))
        {
            return Error.Validation(
                "APPLICATION_BUILD_PROFILE_UNAVAILABLE",
                "The project's pinned catalog does not contain the Container build profile.");
        }

        var descriptors = profile.Steps
            .Select(stepName => catalog.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Name, stepName, StringComparison.Ordinal)))
            .OfType<CatalogPipelineStepDescriptor>()
            .ToArray();
        var stepFindings = ApplicationStepValidator.Validate(command.Definition.Steps, descriptors);
        if (stepFindings.Count > 0)
        {
            return Error.Validation("VAL-APP-ETAPE", stepFindings[0].Message,
                new Dictionary<string, object> { ["step"] = stepFindings[0].StepName });
        }

        var snapshot = await snapshotBuilder.BuildAsync(command.ProjectId, cancellationToken);
        if (snapshot.IsError)
        {
            return snapshot.Errors;
        }

        var registry = BuildRegistryResolver.Resolve(
            snapshot.Value,
            resource.Id,
            linkKinds,
            command.Definition.BuildRegistryEnvironmentId);
        if (registry.ComponentCode is not null && !registry.HasValidSelection)
        {
            return Error.Validation(
                "APPLICATION_BUILD_REGISTRY_REQUIRED",
                "Select a build registry from an ImagePull link before saving the application.");
        }

        var definition = command.Definition with
        {
            BuildRegistryEnvironmentId = registry.SelectedEnvironmentId
        };
        var existing = await dbContext.Query<ApplicationAggregate>()
            .Where(application => application.ProjectId == project.Id
                && application.ComponentId == component.Id)
            .ToArrayAsync(cancellationToken);
        var application = existing.SingleOrDefault(candidate => candidate.ResourceId == resource.Id);
        if (application is null)
        {
            if (command.ExpectedVersion != 0)
            {
                return Error.Conflict("CONFLICT_VERSION", "The application changed. Reload it before saving.",
                    new Dictionary<string, object> { ["currentVersion"] = 0 });
            }

            var created = ApplicationAggregate.Create(
                resource.Id,
                project.Id,
                project.OrganizationId,
                component.Id,
                resource.Id,
                definition,
                existing);
            if (created.IsError)
            {
                return created.Errors;
            }

            dbContext.Query<ApplicationAggregate>().Add(created.Value);
            return new ModelCommandOutcome($"Application {created.Value.Name} configured.", []);
        }

        if (application.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The application changed. Reload it before saving.",
                new Dictionary<string, object> { ["currentVersion"] = application.Version });
        }

        var updated = application.Update(definition, existing);
        return updated.IsError
            ? updated.Errors
            : new ModelCommandOutcome($"Application {application.Name} configured.", []);
    }
}
