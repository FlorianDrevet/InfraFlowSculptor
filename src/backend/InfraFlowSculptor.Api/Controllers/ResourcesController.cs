using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Application.Components.Commands.AddChild;
using InfraFlowSculptor.Application.Components.Commands.AddResource;
using InfraFlowSculptor.Application.Components.Commands.ClearOverride;
using InfraFlowSculptor.Application.Components.Commands.DeleteResource;
using InfraFlowSculptor.Application.Components.Commands.MoveResource;
using InfraFlowSculptor.Application.Components.Commands.RemoveChild;
using InfraFlowSculptor.Application.Components.Commands.SetExistingResourceId;
using InfraFlowSculptor.Application.Components.Commands.SetForcedName;
using InfraFlowSculptor.Application.Components.Commands.SetOverride;
using InfraFlowSculptor.Application.Components.Commands.SetPresence;
using InfraFlowSculptor.Application.Components.Commands.SetResourceProperties;
using InfraFlowSculptor.Application.Components.Commands.UpdateResourceMetadata;
using InfraFlowSculptor.Application.Components.Commands.UpdateChild;
using InfraFlowSculptor.Application.Components.Queries.GetEffectiveValues;
using InfraFlowSculptor.Application.Components.Queries.GetResourceDeletionImpact;
using InfraFlowSculptor.Application.Components.Queries.GetProjectComponent;
using InfraFlowSculptor.Application.Components.Queries.PreviewResourceName;
using InfraFlowSculptor.Contracts.Components;
using InfraFlowSculptor.Contracts.Naming;
using InfraFlowSculptor.Contracts.Resources;
using InfraFlowSculptor.Domain.Common.Features;
using InfraFlowSculptor.Engine.Naming;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace InfraFlowSculptor.Api.Controllers;

public static class ResourcesController
{
    public static RouteGroupBuilder MapResourceEndpoints(this RouteGroupBuilder v1)
    {
        var resources = v1.MapGroup("/projects/{projectId:guid}/components/{componentId:guid}/resources")
            .WithTags("Resources")
            .RequireFeature(Features.Organizations)
            .RequireFeature(Features.ProjectSetup);
        var effectiveValues = v1.MapGroup("/projects/{projectId:guid}/resources")
            .WithTags("Resources")
            .RequireFeature(Features.Organizations)
            .RequireFeature(Features.ProjectSetup);

        resources.MapPost("/preview-name", async (
                Guid projectId,
                Guid componentId,
                PreviewResourceNameRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new PreviewResourceNameQuery(
                    projectId,
                    componentId,
                    request.ResourceGroupId,
                    request.Type,
                    request.LogicalName,
                    request.ResourceId), cancellationToken);
                return result.Match<IResult>(
                    preview => Results.Ok(MapPreview(preview)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.PreviewResourceName)
            .WithSummary("Calculer les noms Azure d'une ressource avant son ajout au modèle.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<ProjectNamingPreviewResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        resources.MapGet("/{resourceId:guid}/delete-impact", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new GetResourceDeletionImpactQuery(projectId, componentId, resourceId),
                    cancellationToken);
                return result.Match<IResult>(
                    summary => Results.Ok(new ResourceDeletionImpactResponse(
                        summary.ResourceVersion,
                        ResourceDeletionImpactStatus.Partial,
                        [
                            ResourceDeletionImpactArea.IncomingLinks,
                            ResourceDeletionImpactArea.ApplicationParameters,
                            ResourceDeletionImpactArea.ImplicitEffects
                        ],
                        summary.Items.Select(item => new ResourceDeletionImpactItemResponse(
                            ResourceDeletionImpactKind.Child,
                            item.Type,
                            item.Name)).ToArray())),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetResourceDeletionImpact)
            .WithSummary("Prévisualiser les effets calculés avant la suppression d'une ressource.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<ResourceDeletionImpactResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        effectiveValues.MapGet("/{resourceId:guid}/effective-values", async (
                Guid projectId,
                Guid resourceId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetEffectiveValuesQuery(projectId, resourceId), cancellationToken);
                return result.Match<IResult>(
                    values => Results.Ok(values.Select(Map).ToArray()),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetEffectiveValues)
            .WithSummary("Consulter les valeurs effectives d'une ressource par cible.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<EffectiveValueTargetResponse[]>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        resources.MapPost("", async (
                Guid projectId,
                Guid componentId,
                SaveResourceRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                TimeProvider clock,
                CancellationToken cancellationToken) =>
            {
                var resourceId = Guid.CreateVersion7(clock.GetUtcNow());
                var command = new AddResourceCommand(
                    projectId,
                    componentId,
                    resourceId,
                    request.ResourceGroupId,
                    request.Type,
                    request.LogicalName,
                    request.Description,
                    request.IsExisting,
                    request.Properties,
                    request.Tags,
                    request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status201Created, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.AddResource)
            .WithSummary("Ajouter une ressource pilotée par son descripteur.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapPut("/{resourceId:guid}/properties", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                SaveResourcePropertiesRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new SetResourcePropertiesCommand(
                    projectId, componentId, resourceId, request.Properties, request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status200OK, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.SetResourceProperties)
            .WithSummary("Modifier les propriétés de base d'une ressource.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapPut("/{resourceId:guid}/metadata", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                UpdateResourceMetadataRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateResourceMetadataCommand(
                    projectId,
                    componentId,
                    resourceId,
                    request.Description,
                    request.Tags,
                    request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status200OK, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.UpdateResourceMetadata)
            .WithSummary("Modifier la description et les tags d'une ressource.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapPut("/{resourceId:guid}/overrides", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                SetResourceOverrideRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new SetOverrideCommand(
                    projectId, componentId, resourceId, request.TargetId, request.Property, request.Value, request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status200OK, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.SetOverride)
            .WithSummary("Définir une surcharge de propriété pour un environnement.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapDelete("/{resourceId:guid}/overrides", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                [FromBody] ClearResourceOverrideRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new ClearOverrideCommand(
                    projectId, componentId, resourceId, request.TargetId, request.Property, request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status200OK, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.ClearOverride)
            .WithSummary("Effacer une surcharge et rétablir l'héritage.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapPut("/{resourceId:guid}/presence", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                SetResourcePresenceRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new SetPresenceCommand(
                    projectId, componentId, resourceId, request.TargetId, request.IsPresent, request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status200OK, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.SetPresence)
            .WithSummary("Définir la présence d'une ressource dans un environnement.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapPut("/{resourceId:guid}/existing-id", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                SetExistingResourceIdRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new SetExistingResourceIdCommand(
                    projectId, componentId, resourceId, request.TargetId, request.ResourceId, request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status200OK, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.SetExistingResourceId)
            .WithSummary("Définir l'identifiant Azure d'une ressource existante.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapPut("/{resourceId:guid}/forced-name", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                SetResourceForcedNameRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new SetForcedNameCommand(
                    projectId, componentId, resourceId, request.TargetId, request.Name, request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status200OK, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.SetForcedName)
            .WithSummary("Forcer le nom Azure d'une ressource pour un environnement.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapPut("/{resourceId:guid}/resource-group", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                MoveResourceRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new MoveResourceCommand(
                    projectId, componentId, resourceId, request.ResourceGroupId, request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status200OK, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.MoveResource)
            .WithSummary("Déplacer une ressource dans un autre groupe.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapDelete("/{resourceId:guid}", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                [FromBody] ExpectedVersionRequest request,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new DeleteResourceCommand(projectId, componentId, resourceId, request.ExpectedVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(_ => Results.NoContent(), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.DeleteResource)
            .WithSummary("Supprimer une ressource du modèle.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapPost("/{resourceId:guid}/children", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                SaveResourceChildRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                TimeProvider clock,
                CancellationToken cancellationToken) =>
            {
                var command = new AddChildCommand(
                    projectId, componentId, resourceId, Guid.CreateVersion7(clock.GetUtcNow()),
                    request.Type, request.Name, request.Properties, request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status200OK, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.AddChild)
            .WithSummary("Ajouter un enfant défini par le descripteur de la ressource.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapPut("/{resourceId:guid}/children/{childId:guid}", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                Guid childId,
                SaveResourceChildRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateChildCommand(
                    projectId, componentId, resourceId, childId, request.Name, request.Properties, request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status200OK, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.UpdateChild)
            .WithSummary("Modifier un enfant de la ressource.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapDelete("/{resourceId:guid}/children/{childId:guid}", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                Guid childId,
                [FromBody] ExpectedVersionRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new RemoveChildCommand(
                    projectId, componentId, resourceId, childId, request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status200OK, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.RemoveChild)
            .WithSummary("Supprimer un enfant de la ressource.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return resources;
    }

    private static async Task<IResult> ExecuteAndReturnResourceAsync(
        IModelCommand command,
        int statusCode,
        Guid projectId,
        Guid componentId,
        Guid resourceId,
        IModelCommandExecutor executor,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var executed = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
        if (executed.IsError)
        {
            return executed.Errors.ToProblem();
        }

        var result = await mediator.Send(new GetProjectComponentQuery(projectId, componentId), cancellationToken);
        return result.Match<IResult>(
            component => component.Resources.SingleOrDefault(resource => resource.Id == resourceId) is { } resource
                ? statusCode == StatusCodes.Status201Created
                    ? Results.Created($"/v1/projects/{projectId:D}/components/{componentId:D}/resources/{resourceId:D}", Map(resource))
                    : Results.Ok(Map(resource))
                : Results.NotFound(),
            errors => errors.ToProblem());
    }

    private static ResourceResponse Map(ResourceSummary resource) => new(
        resource.Id,
        resource.Version,
        resource.ResourceGroupId,
        resource.Type,
        resource.LogicalName,
        resource.Description,
        resource.IsExisting,
        resource.Properties,
        resource.Overrides,
        resource.Presences,
        resource.ExistingIds,
        resource.ForcedNames,
        resource.Tags,
        resource.Children.Select(child => new ResourceChildResponse(
            child.Id,
            child.Type,
            child.Name,
            child.Properties)).ToArray());

    private static EffectiveValueTargetResponse Map(EffectiveValueTargetSummary target) => new(
        target.Id,
        target.Code,
        target.IsPresent,
        target.Region,
        target.Tags,
        target.Properties.Select(property => new EffectivePropertyResponse(
            property.Name,
            property.Value,
            property.Provenance)).ToArray());

    private static ProjectNamingPreviewResponse MapPreview(NamingPreviewResult preview) => new(
        preview.Items.Select(item => new ProjectNamingPreviewItemResponse(
            item.ComponentId,
            item.ComponentCode,
            item.ResourceId,
            item.ResourceType,
            item.LogicalName,
            item.TargetId,
            item.EnvironmentCode,
            item.ResourceName,
            item.ResourceGroupName,
            item.NameLength,
            item.MaximumLength,
            item.IsExisting,
            MapExplanation(item.Explanation),
            MapExplanation(item.ResourceGroupExplanation))).ToArray(),
        preview.Issues.Select(issue => new ProjectNamingIssueResponse(
            issue.Code,
            issue.Message,
            issue.Severity,
            issue.ComponentId,
            issue.ResourceId,
            issue.ResourceGroupId,
            issue.TargetId,
            issue.Value,
            issue.ValueLength,
            issue.Limit)).ToArray());

    private static NamingExplanationResponse? MapExplanation(NamingExplanation? explanation) =>
        explanation is null
            ? null
            : new NamingExplanationResponse(
                explanation.Level,
                explanation.Template,
                explanation.RawName,
                explanation.FinalName,
                explanation.Transformations);
}
