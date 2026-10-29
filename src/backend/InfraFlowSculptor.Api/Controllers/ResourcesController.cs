using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Errors;
using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Application.Components.Commands.AddChild;
using InfraFlowSculptor.Application.Components.Commands.CreateAppSetting;
using InfraFlowSculptor.Application.Components.Commands.DeleteAppSetting;
using InfraFlowSculptor.Application.Components.Commands.AddLink;
using InfraFlowSculptor.Application.Components.Commands.AddResource;
using InfraFlowSculptor.Application.Components.Commands.ClearOverride;
using InfraFlowSculptor.Application.Components.Commands.DeleteResource;
using InfraFlowSculptor.Application.Components.Commands.MoveResource;
using InfraFlowSculptor.Application.Components.Commands.RemoveLink;
using InfraFlowSculptor.Application.Components.Commands.RemoveChild;
using InfraFlowSculptor.Application.Components.Commands.SetExistingResourceId;
using InfraFlowSculptor.Application.Components.Commands.SetForcedName;
using InfraFlowSculptor.Application.Components.Commands.SetOverride;
using InfraFlowSculptor.Application.Components.Commands.SetPresence;
using InfraFlowSculptor.Application.Components.Commands.SetResourceProperties;
using InfraFlowSculptor.Application.Components.Commands.SetResourceDefaultIdentity;
using InfraFlowSculptor.Application.Components.Commands.UpdateResourceMetadata;
using InfraFlowSculptor.Application.Components.Commands.UpdateLink;
using InfraFlowSculptor.Application.Components.Commands.UpdateChild;
using InfraFlowSculptor.Application.Components.Commands.UpdateAppSetting;
using InfraFlowSculptor.Application.Components.Queries.GetAppSettings;
using InfraFlowSculptor.Application.Components.Queries.GetEffectiveValues;
using InfraFlowSculptor.Application.Components.Queries.GetResourceDeletionImpact;
using InfraFlowSculptor.Application.Components.Queries.GetResourceApplication;
using InfraFlowSculptor.Application.Components.Commands.SaveApplication;
using InfraFlowSculptor.Application.Components.Queries.GetResourceLinks;
using InfraFlowSculptor.Application.Components.Queries.GetLinkRemovalImpact;
using InfraFlowSculptor.Application.Components.Queries.GetResourceLinkOptions;
using InfraFlowSculptor.Application.Components.Queries.PreviewLink;
using InfraFlowSculptor.Application.Components.Queries.GetProjectComponent;
using InfraFlowSculptor.Application.Components.Queries.PreviewResourceName;
using InfraFlowSculptor.Contracts.Components;
using InfraFlowSculptor.Contracts.Naming;
using InfraFlowSculptor.Contracts.Resources;
using InfraFlowSculptor.Domain.Common.Features;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Engine.Naming;
using InfraFlowSculptor.Engine.Applications;
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

        resources.MapGet("/{resourceId:guid}/application", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new GetResourceApplicationQuery(projectId, componentId, resourceId),
                    cancellationToken);
                return result.Match<IResult>(summary => Results.Ok(MapApplication(summary)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetResourceApplication)
            .WithSummary("Consulter le profil de build d'une application conteneur.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<ResourceApplicationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        resources.MapPut("/{resourceId:guid}/application", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                SaveApplicationRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var definition = ToApplicationDefinition(request);
                if (definition.IsError)
                {
                    return definition.Errors.ToProblem();
                }

                var command = new SaveApplicationCommand(
                    projectId, componentId, resourceId, definition.Value, request.ExpectedVersion);
                var executed = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                if (executed.IsError)
                {
                    return executed.Errors.ToProblem();
                }

                var result = await mediator.Send(
                    new GetResourceApplicationQuery(projectId, componentId, resourceId),
                    cancellationToken);
                return result.Match<IResult>(summary => Results.Ok(MapApplication(summary)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.SaveResourceApplication)
            .WithSummary("Enregistrer les réglages de build d'une application conteneur.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceApplicationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapGet("/{resourceId:guid}/app-settings", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetAppSettingsQuery(projectId, componentId, resourceId), cancellationToken);
                return result.Match<IResult>(
                    settings => Results.Ok(settings.Select(Map).ToArray()),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetAppSettings)
            .WithSummary("Lister les paramètres applicatifs d'une ressource.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<AppSettingResponse[]>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        resources.MapPost("/{resourceId:guid}/app-settings", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                AppSettingRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                TimeProvider clock,
                CancellationToken cancellationToken) =>
            {
                var settingId = Guid.CreateVersion7(clock.GetUtcNow());
                var definition = ToDefinition(request);
                var command = definition.ForceNonSecret
                    ? (InfraFlowSculptor.Application.Common.ModelCommands.IModelCommand)new ForceNonSecretAppSettingCommand(
                        projectId, componentId, resourceId, settingId, definition)
                    : new CreateAppSettingCommand(projectId, componentId, resourceId, settingId, definition);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                if (result.IsError)
                {
                    return result.Errors.ToProblem();
                }

                var created = await mediator.Send(new GetAppSettingsQuery(projectId, componentId, resourceId), cancellationToken);
                return created.Match<IResult>(
                    settings => settings.SingleOrDefault(setting => setting.Id == settingId) is { } setting
                        ? Results.Created($"/v1/projects/{projectId:D}/components/{componentId:D}/resources/{resourceId:D}/app-settings/{settingId:D}", MapMutation(setting))
                        : Results.NotFound(),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.CreateAppSetting)
            .WithSummary("Ajouter un paramètre applicatif sans transmettre de secret en clair.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<AppSettingMutationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapPut("/{resourceId:guid}/app-settings/{appSettingId:guid}", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                Guid appSettingId,
                AppSettingUpdateRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var definition = ToDefinition(request);
                var command = definition.ForceNonSecret
                    ? (InfraFlowSculptor.Application.Common.ModelCommands.IModelCommand)new ForceNonSecretAppSettingUpdateCommand(
                        projectId, componentId, resourceId, appSettingId, definition, request.ExpectedVersion)
                    : new UpdateAppSettingCommand(
                        projectId, componentId, resourceId, appSettingId, definition, request.ExpectedVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                if (result.IsError)
                {
                    return result.Errors.ToProblem();
                }

                var updated = await mediator.Send(new GetAppSettingsQuery(projectId, componentId, resourceId), cancellationToken);
                return updated.Match<IResult>(
                    settings => settings.SingleOrDefault(setting => setting.Id == appSettingId) is { } setting
                        ? Results.Ok(MapMutation(setting))
                        : Results.NotFound(),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.UpdateAppSetting)
            .WithSummary("Modifier un paramètre applicatif.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<AppSettingMutationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapDelete("/{resourceId:guid}/app-settings/{appSettingId:guid}", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                Guid appSettingId,
                [FromBody] DeleteAppSettingRequest request,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new DeleteAppSettingCommand(
                    projectId, componentId, resourceId, appSettingId, request.ExpectedVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(
                    execution => Results.NoContent(),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.DeleteAppSetting)
            .WithSummary("Supprimer un paramètre applicatif.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

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
                        summary.LinkImpact.Severity == ImpactSeverity.Blocker || summary.HasBlockingApplicationSettings
                            ? ResourceDeletionImpactStatus.Blocked
                            : ResourceDeletionImpactStatus.Partial,
                        summary.HasBlockingApplicationSettings
                            ? ResourceImpactSeverity.Blocker
                            : MapSeverity(summary.LinkImpact.Severity),
                        [ResourceDeletionImpactArea.ImplicitEffects],
                        summary.Items.Select(item => new ResourceDeletionImpactItemResponse(
                                item.Type == "Application setting"
                                    ? ResourceDeletionImpactKind.ApplicationSetting
                                    : ResourceDeletionImpactKind.Child,
                                item.Type,
                                item.Name,
                                item.Code,
                                item.BlocksDeletion ? ResourceImpactSeverity.Blocker : ResourceImpactSeverity.Information,
                                item.Message,
                                null,
                                resourceId))
                            .Concat(summary.LinkImpact.Items.Select(item => new ResourceDeletionImpactItemResponse(
                                ResourceDeletionImpactKind.Impact,
                                "Impact",
                                item.Message,
                                item.Code,
                                MapSeverity(item.Severity),
                                item.Message,
                                item.LinkId,
                                item.ResourceId)))
                            .ToArray())),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetResourceDeletionImpact)
            .WithSummary("Prévisualiser les effets calculés avant la suppression d'une ressource.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<ResourceDeletionImpactResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        resources.MapGet("/{resourceId:guid}/links/options", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new GetResourceLinkOptionsQuery(projectId, componentId, resourceId),
                    cancellationToken);
                return result.Match<IResult>(
                    options => Results.Ok(MapOptions(options)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetResourceLinkOptions)
            .WithSummary("Lister les types, cibles et champs de liaison compatibles avec une ressource.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<ResourceLinkOptionsResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        resources.MapGet("/{resourceId:guid}/links", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new GetResourceLinksQuery(projectId, componentId, resourceId),
                    cancellationToken);
                return result.Match<IResult>(
                    summary => Results.Ok(new ResourceLinksResponse(
                        summary.Outgoing.Select(Map).ToArray(),
                        summary.Incoming.Select(Map).ToArray())),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetResourceLinks)
            .WithSummary("Lister les liaisons sortantes et entrantes explicites et implicites d'une ressource.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<ResourceLinksResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        resources.MapPost("/{resourceId:guid}/links/preview", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                LinkPreviewRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new PreviewLinkQuery(
                    projectId,
                    componentId,
                    resourceId,
                    request.TargetComponentId,
                    request.TargetResourceId,
                    request.Kind,
                    request.IdentityKind,
                    request.IdentityResourceId,
                    request.Roles,
                    request.AccessScope,
                    request.DataAccessLevel), cancellationToken);
                return result.Match<IResult>(
                    preview => Results.Ok(MapPreview(preview)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.PreviewLink)
            .WithSummary("Calculer les effets d'une liaison sans modifier le modèle.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<LinkPreviewResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        resources.MapGet("/{resourceId:guid}/links/{linkId:guid}/impact", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                Guid linkId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new GetLinkRemovalImpactQuery(projectId, componentId, resourceId, linkId),
                    cancellationToken);
                return result.Match<IResult>(
                    impact => Results.Ok(new LinkImpactResponse(
                        impact.ModelVersion,
                        MapSeverity(impact.Severity),
                        impact.Items.Select(item => new LinkImpactItemResponse(
                            item.Code,
                            MapSeverity(item.Severity),
                            item.Message,
                            item.LinkId,
                            item.ResourceId)).ToArray())),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetLinkRemovalImpact)
            .WithSummary("Prévisualiser les effets du retrait d'une liaison.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<LinkImpactResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        resources.MapPost("/{resourceId:guid}/links", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                AddLinkRequest request,
                IModelCommandExecutor executor,
                TimeProvider clock,
                CancellationToken cancellationToken) =>
            {
                var linkId = Guid.CreateVersion7(clock.GetUtcNow());
                var command = new AddLinkCommand(
                    projectId,
                    componentId,
                    linkId,
                    resourceId,
                    request.TargetComponentId,
                    request.TargetResourceId,
                    request.Kind,
                    request.IdentityKind,
                    request.IdentityResourceId,
                    request.Roles,
                    request.AccessScope,
                    request.DataAccessLevel,
                    request.ExpectedComponentVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(
                    execution => Results.Created(
                        $"/v1/projects/{projectId:D}/components/{componentId:D}/resources/{resourceId:D}/links/{linkId:D}",
                        new LinkMutationResponse(linkId, execution.Summary, execution.Effects, execution.Findings)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.AddLink)
            .WithSummary("Créer une liaison explicite entre deux ressources du projet.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<LinkMutationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapPut("/{resourceId:guid}/links/{linkId:guid}", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                Guid linkId,
                UpdateLinkRequest request,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateLinkCommand(
                    projectId,
                    componentId,
                    linkId,
                    resourceId,
                    request.TargetComponentId,
                    request.TargetResourceId,
                    request.Kind,
                    request.IdentityKind,
                    request.IdentityResourceId,
                    request.Roles,
                    request.AccessScope,
                    request.DataAccessLevel,
                    request.ExpectedLinkVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(
                    execution => Results.Ok(new LinkMutationResponse(linkId, execution.Summary, execution.Effects, execution.Findings)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.UpdateLink)
            .WithSummary("Modifier une liaison explicite.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<LinkMutationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        resources.MapDelete("/{resourceId:guid}/links/{linkId:guid}", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                Guid linkId,
                [FromBody] RemoveLinkRequest request,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new RemoveLinkCommand(
                    projectId,
                    componentId,
                    resourceId,
                    linkId,
                    request.ExpectedLinkVersion,
                    request.ExpectedModelVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(
                    execution => Results.Ok(new LinkMutationResponse(linkId, execution.Summary, execution.Effects, execution.Findings)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.RemoveLink)
            .WithSummary("Retirer une liaison explicite après examen de son impact.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<LinkMutationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

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
                    request.ExpectedVersion)
                {
                    DefaultIdentityResourceId = request.DefaultIdentityResourceId
                };
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

        resources.MapPut("/{resourceId:guid}/default-identity", async (
                Guid projectId,
                Guid componentId,
                Guid resourceId,
                SetResourceDefaultIdentityRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var command = new SetResourceDefaultIdentityCommand(
                    projectId, componentId, resourceId, request.IdentityResourceId, request.ExpectedVersion);
                return await ExecuteAndReturnResourceAsync(
                    command, StatusCodes.Status200OK, projectId, componentId, resourceId, executor, mediator, cancellationToken);
            })
            .WithName(EndpointNames.SetResourceDefaultIdentity)
            .WithSummary("Définir l'identité affectée par défaut d'une application.")
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
            child.Properties)).ToArray(),
        resource.DefaultIdentityResourceId);

    private static ResourceLinkResponse Map(ResourceLinkSummary link) => new(
        link.Id,
        link.Kind,
        link.KindLabelFr,
        link.KindLabelEn,
        link.Origin,
        link.SourceComponentId,
        link.SourceComponentCode,
        link.SourceResourceId,
        link.SourceResourceName,
        link.TargetComponentId,
        link.TargetComponentCode,
        link.TargetResourceId,
        link.TargetResourceName,
        link.IdentityKind,
        link.IdentityResourceId,
        link.AccessScope,
        link.DataAccessLevel,
        link.Roles,
        link.IsExternal,
        link.CreatesDeploymentDependency,
        link.Findings.Select(finding => new ResourceLinkIssueResponse(finding.Code, finding.Message)).ToArray(),
        link.Version,
        link.OriginSource is null
            ? null
            : new LinkOriginSourceResponse(link.OriginSource.Kind, link.OriginSource.Id, link.OriginSource.Name));

    private static ResourceLinkOptionsResponse MapOptions(ResourceLinkOptionsSummary options) => new(
        options.Kinds.Select(kind => new ResourceLinkKindOptionResponse(
            kind.Kind,
            kind.LabelFr,
            kind.LabelEn,
            kind.Fields.Select(field => new ResourceLinkFormFieldResponse(
                field.Name,
                field.Kind,
                field.Required)).ToArray(),
            kind.Targets.Select(target => new ResourceLinkTargetOptionResponse(
                target.ComponentId,
                target.ComponentCode,
                target.ResourceId,
                target.ResourceName,
                target.ResourceType,
                target.IsExisting,
                target.Roles,
                target.DataAccessLevels,
                target.Identities.Select(identity => new ResourceLinkIdentityOptionResponse(
                    identity.Kind,
                    identity.ResourceId,
                    identity.ResourceName,
                    identity.IsDefault)).ToArray())).ToArray())).ToArray());

    private static LinkPreviewResponse MapPreview(LinkPreviewResult preview) => new(
        preview.RoleAssignments.Select(effect => new LinkPreviewRoleAssignmentResponse(
            effect.EnvironmentId,
            effect.EnvironmentCode,
            effect.PrincipalResourceId,
            effect.PrincipalResourceName,
            effect.RoleName,
            effect.Scope,
            effect.ScopeResourceId,
            effect.ScopeResourceName,
            effect.TargetResourceId,
            effect.TargetResourceName,
            effect.IsReused)).ToArray(),
        preview.Identities.Select(effect => new LinkPreviewIdentityResponse(
            effect.EnvironmentId,
            effect.EnvironmentCode,
            effect.ResourceId,
            effect.ResourceName,
            effect.Kind,
            effect.IdentityResourceId,
            effect.IdentityResourceName,
            effect.IsReused)).ToArray(),
        preview.DataAccess.Select(effect => new LinkPreviewDataAccessResponse(
            effect.EnvironmentId,
            effect.EnvironmentCode,
            effect.SourceResourceId,
            effect.SourceResourceName,
            effect.TargetResourceId,
            effect.TargetResourceName,
            effect.Level,
            effect.DatabaseRoles)).ToArray(),
        preview.Dependencies.Select(effect => new LinkPreviewDependencyResponse(
            effect.EnvironmentId,
            effect.EnvironmentCode,
            effect.SourceComponentId,
            effect.SourceComponentCode,
            effect.TargetComponentId,
            effect.TargetComponentCode,
            effect.Kind)).ToArray(),
        preview.ApplicationSettings.Select(effect => new LinkPreviewApplicationSettingResponse(
            effect.EnvironmentId,
            effect.EnvironmentCode,
            effect.ApplicationResourceId,
            effect.ApplicationResourceName,
            effect.Name,
            effect.Origin,
            effect.IsSensitive)).ToArray(),
        preview.Findings.Select(finding => new LinkPreviewFindingResponse(
            finding.Code,
            finding.Message,
            finding.ResourceId)).ToArray());

    private static AppSettingResponse Map(AppSettingSummary setting) => new(
        setting.Id,
        setting.Version,
        setting.Name,
        setting.SourceKind,
        setting.LiteralValue,
        setting.Overrides,
        setting.SourceResourceId,
        setting.OutputName,
        setting.KeyVaultResourceId,
        setting.SecretName,
        setting.SecretFeedKind,
        setting.PipelineVariableName,
        setting.IdentityResourceId,
        setting.SecretValueDeclaredNonSecret,
        setting.Warnings,
        setting.IsImplicit,
        setting.Origin);

    private static AppSettingMutationResponse MapMutation(AppSettingSummary setting) => new(
        setting.Id,
        setting.Version,
        setting.Name,
        setting.SourceKind,
        setting.SourceResourceId,
        setting.OutputName,
        setting.KeyVaultResourceId,
        setting.SecretName,
        setting.SecretFeedKind,
        setting.PipelineVariableName,
        setting.IdentityResourceId,
        setting.SecretValueDeclaredNonSecret,
        setting.Warnings,
        setting.IsImplicit,
        setting.Origin);

    private static InfraFlowSculptor.Domain.ComponentAggregate.AppSettingDefinition ToDefinition(AppSettingRequest request) => new(
        request.Name,
        request.SourceKind,
        request.LiteralValue,
        request.Overrides,
        request.SourceResourceId,
        request.OutputName,
        request.KeyVaultResourceId,
        request.SecretName,
        request.SecretFeedKind,
        request.PipelineVariableName,
        request.IdentityResourceId,
        request.ForceNonSecret);

    private static InfraFlowSculptor.Domain.ComponentAggregate.AppSettingDefinition ToDefinition(AppSettingUpdateRequest request) => new(
        request.Name,
        request.SourceKind,
        request.LiteralValue,
        request.Overrides,
        request.SourceResourceId,
        request.OutputName,
        request.KeyVaultResourceId,
        request.SecretName,
        request.SecretFeedKind,
        request.PipelineVariableName,
        request.IdentityResourceId,
        request.ForceNonSecret);

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

    private static ResourceApplicationResponse MapApplication(ResourceApplicationSummary summary)
    {
        if (!summary.Available || summary.Definition is null || summary.BuildRegistry is null)
        {
            return new ResourceApplicationResponse(
                false,
                summary.CatalogVersion,
                summary.BuildProfile,
                summary.UnavailableReason,
                null);
        }

        var descriptors = summary.StepDescriptors.ToDictionary(step => step.Name, StringComparer.Ordinal);
        var steps = summary.Definition.Steps.Select(step =>
        {
            descriptors.TryGetValue(step.Name, out var descriptor);
            var parameters = step.Parameters.Select(parameter =>
            {
                var parameterDescriptor = descriptor?.Parameters.SingleOrDefault(candidate =>
                    string.Equals(candidate.Name, parameter.Name, StringComparison.Ordinal));
                return new ApplicationStepParameterResponse(
                    parameter.Name,
                    parameterDescriptor?.Kind ?? "string",
                    parameter.Value,
                    parameterDescriptor?.DefaultValue,
                    parameterDescriptor?.Values ?? []);
            }).ToArray();
            return new ApplicationStepResponse(
                step.Name,
                descriptor?.LabelFr ?? step.Name,
                descriptor?.Description ?? string.Empty,
                step.Enabled,
                step.Blocking,
                step.Position.ToString(),
                parameters);
        }).ToArray();
        var registry = summary.BuildRegistry;
        return new ResourceApplicationResponse(
            true,
            summary.CatalogVersion,
            summary.BuildProfile,
            null,
            new ApplicationConfigurationResponse(
                summary.SavedApplication?.Version ?? 0,
                summary.Definition.Name,
                summary.Definition.CodeSourcePath,
                summary.Definition.DockerfilePath,
                summary.Definition.BuildContextPath,
                summary.Definition.ImageRepository,
                summary.Definition.HealthCheckPath,
                summary.Definition.DeploymentStrategy.ToString(),
                new ApplicationBuildRegistryResponse(
                    registry.IsAvailable,
                    registry.DisplayName,
                    registry.RegistryName,
                    registry.PromotionRequired,
                    registry.SelectedEnvironmentId,
                    registry.HasValidSelection,
                    registry.Options.Select(option => new ApplicationBuildRegistryOptionResponse(
                        option.EnvironmentId,
                        option.EnvironmentCode,
                        option.RegistryName)).ToArray()),
                steps,
                summary.Findings.Select(finding => new ApplicationFindingResponse(
                    finding.Code,
                    finding.StepName,
                    finding.ParameterName,
                    finding.Message)).ToArray()));
    }

    private static ErrorOr<InfraFlowSculptor.Domain.ComponentAggregate.ApplicationDefinition> ToApplicationDefinition(
        SaveApplicationRequest request)
    {
        if (!Enum.TryParse<DeploymentStrategyKind>(request.DeploymentStrategy, true, out var strategy)
            || !Enum.IsDefined(strategy))
        {
            return Error.Validation("APPLICATION_DEPLOYMENT_STRATEGY_INVALID", "The deployment strategy is unsupported.");
        }

        var steps = new List<ApplicationStepDefinition>();
        foreach (var step in request.Steps ?? [])
        {
            if (!Enum.TryParse<ApplicationStepPosition>(step.Position, true, out var position)
                || !Enum.IsDefined(position))
            {
                return Error.Validation("APPLICATION_STEP_INVALID", "The application step position is unsupported.");
            }

            steps.Add(new ApplicationStepDefinition(
                step.Name,
                step.Enabled,
                step.Blocking,
                position,
                (step.Parameters ?? []).Select(parameter => new ApplicationStepParameter(parameter.Name, parameter.Value)).ToArray()));
        }

        return new InfraFlowSculptor.Domain.ComponentAggregate.ApplicationDefinition(
            request.Name,
            request.CodeSourcePath,
            request.DockerfilePath,
            request.BuildContextPath,
            request.ImageRepository,
            request.HealthCheckPath,
            strategy,
            request.BuildRegistryEnvironmentId,
            steps);
    }

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

    private static ResourceImpactSeverity MapSeverity(ImpactSeverity severity) => severity switch
    {
        ImpactSeverity.Blocker => ResourceImpactSeverity.Blocker,
        ImpactSeverity.Critical => ResourceImpactSeverity.Critical,
        _ => ResourceImpactSeverity.Information
    };

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
