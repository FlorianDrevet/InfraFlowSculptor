using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Applications;
using InfraFlowSculptor.Engine.Links;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Queries.GetResourceApplication;

public sealed class GetResourceApplicationQueryHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ModelSnapshotBuilder snapshotBuilder,
    LinkKindRegistry linkKinds,
    ICatalogProvider catalogProvider)
    : IQueryHandler<GetResourceApplicationQuery, ErrorOr<ResourceApplicationSummary>>
{
    public async ValueTask<ErrorOr<ResourceApplicationSummary>> Handle(
        GetResourceApplicationQuery query,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(
            dbContext, currentOrganization, query.ProjectId, query.ComponentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var (project, component) = loaded.Value;
        var resource = component.Resources.SingleOrDefault(candidate => candidate.Id == query.ResourceId);
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
            return new ResourceApplicationSummary(
                false,
                catalog.Version,
                buildProfileName,
                "Le catalogue épinglé ne contient pas le profil de build Conteneur.",
                null,
                null,
                null,
                [],
                []);
        }

        var saved = await dbContext.Query<ApplicationAggregate>()
            .SingleOrDefaultAsync(application => application.ProjectId == project.Id
                && application.ComponentId == component.Id
                && application.ResourceId == resource.Id,
                cancellationToken);
        var descriptors = profile.Steps
            .Select(stepName => catalog.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.Name, stepName, StringComparison.Ordinal)))
            .OfType<CatalogPipelineStepDescriptor>()
            .ToArray();
        var definition = saved is null
            ? CreateDefaultDefinition(project, component, resource, descriptors)
            : new ApplicationDefinition(
                saved.Name,
                saved.CodeSourcePath,
                saved.DockerfilePath,
                saved.BuildContextPath,
                saved.ImageRepository,
                saved.HealthCheckPath,
                saved.DeploymentStrategy,
                saved.BuildRegistryEnvironmentId,
                saved.Steps);

        var snapshot = await snapshotBuilder.BuildAsync(query.ProjectId, cancellationToken);
        if (snapshot.IsError)
        {
            return snapshot.Errors;
        }

        var registry = BuildRegistryResolver.Resolve(
            snapshot.Value,
            resource.Id,
            linkKinds,
            definition.BuildRegistryEnvironmentId);
        var findings = ApplicationStepValidator.Validate(definition.Steps, descriptors);
        return new ResourceApplicationSummary(
            true,
            catalog.Version,
            profile.Name,
            null,
            saved,
            definition,
            registry,
            descriptors,
            findings);
    }

    private static ApplicationDefinition CreateDefaultDefinition(
        ProjectAggregate project,
        ComponentAggregate component,
        Resource resource,
        IReadOnlyCollection<CatalogPipelineStepDescriptor> descriptors)
    {
        var name = resource.LogicalName.ToLowerInvariant();
        var codeSourcePath = $"src/{name}";
        var steps = descriptors.Select(descriptor => new ApplicationStepDefinition(
            descriptor.Name,
            descriptor.DefaultEnabled,
            descriptor.DefaultBlocking,
            Enum.Parse<ApplicationStepPosition>(descriptor.DefaultPosition, ignoreCase: true),
            descriptor.Parameters.Select(parameter => new ApplicationStepParameter(
                parameter.Name,
                parameter.DefaultValue ?? string.Empty)).ToArray()));
        return new ApplicationDefinition(
            name,
            codeSourcePath,
            $"{codeSourcePath}/Dockerfile",
            codeSourcePath,
            $"{project.Code}/{component.Code}/{name}".ToLowerInvariant(),
            null,
            DeploymentStrategyKind.Direct,
            null,
            steps.ToArray());
    }
}
