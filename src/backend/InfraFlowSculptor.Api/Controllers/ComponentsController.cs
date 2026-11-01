using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Application.Components.Commands.CreateComponent;
using InfraFlowSculptor.Application.Components.Commands.CreateResourceGroup;
using InfraFlowSculptor.Application.Components.Commands.DeleteComponent;
using InfraFlowSculptor.Application.Components.Commands.DeleteResourceGroup;
using InfraFlowSculptor.Application.Components.Commands.UpdateComponent;
using InfraFlowSculptor.Application.Components.Commands.SetComponentDefaultLogAnalytics;
using InfraFlowSculptor.Application.Components.Commands.SetComponentNaming;
using InfraFlowSculptor.Application.Components.Commands.UpdateResourceGroup;
using InfraFlowSculptor.Application.Naming;
using InfraFlowSculptor.Application.Components.Queries.GetProjectComponent;
using InfraFlowSculptor.Application.Components.Queries.ListProjectComponents;
using InfraFlowSculptor.Contracts.Components;
using InfraFlowSculptor.Contracts.Naming;
using InfraFlowSculptor.Domain.Common.Features;
using InfraFlowSculptor.Domain.ComponentAggregate;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace InfraFlowSculptor.Api.Controllers;

public static class ComponentsController
{
    public static RouteGroupBuilder MapComponentEndpoints(this RouteGroupBuilder v1)
    {
        var components = v1.MapGroup("/projects/{projectId:guid}/components")
            .WithTags("Components")
            .RequireFeature(Features.Organizations)
            .RequireFeature(Features.ProjectSetup);

        components.MapGet("", async (Guid projectId, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new ListProjectComponentsQuery(projectId), cancellationToken);
                return result.Match<IResult>(
                    values => Results.Ok(values.Select(Map).ToArray()),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ListProjectComponents)
            .WithSummary("Lister les composants et leurs groupes de ressources.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<IReadOnlyList<ComponentResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        components.MapPost("", async (
                Guid projectId,
                SaveComponentRequest request,
                IMediator mediator,
                IModelCommandExecutor executor,
                TimeProvider clock,
                CancellationToken cancellationToken) =>
            {
                var componentId = Guid.CreateVersion7(clock.GetUtcNow());
                var command = new CreateComponentCommand(
                    projectId,
                    componentId,
                    request.Name,
                    request.Code,
                    request.Description,
                    request.Mode,
                    request.TargetsAllEnvironments,
                    request.TargetEnvironmentIds,
                    request.SingleTarget?.Code,
                    request.SingleTarget?.SubscriptionId,
                    request.SingleTarget?.Region,
                    request.SingleTarget?.DeploymentConnection,
                    request.SingleTarget?.IsProtected,
                    request.SingleTarget?.Approvers,
                    request.SingleTarget?.Runners,
                    request.RemovedResourceBehavior,
                    request.DeletionProtectionEnabled,
                    request.Tags,
                    request.ExpectedVersion);
                var executed = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                if (executed.IsError)
                {
                    return executed.Errors.ToProblem();
                }

                var result = await mediator.Send(new GetProjectComponentQuery(projectId, componentId), cancellationToken);
                return result.Match<IResult>(
                    value => Results.Created($"/v1/projects/{projectId:D}/components/{componentId:D}", Map(value)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.CreateProjectComponent)
            .WithSummary("Créer un composant de déploiement.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ComponentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        components.MapGet("/{componentId:guid}", async (
                Guid projectId,
                Guid componentId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetProjectComponentQuery(projectId, componentId), cancellationToken);
                return result.Match<IResult>(value => Results.Ok(Map(value)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetProjectComponent)
            .WithSummary("Consulter un composant et ses groupes de ressources.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<ComponentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        components.MapPut("/{componentId:guid}", async (
                Guid projectId,
                Guid componentId,
                SaveComponentRequest request,
                IMediator mediator,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateComponentCommand(
                    projectId,
                    componentId,
                    request.Name,
                    request.Code,
                    request.Description,
                    request.Mode,
                    request.TargetsAllEnvironments,
                    request.TargetEnvironmentIds,
                    request.SingleTarget?.Code,
                    request.SingleTarget?.SubscriptionId,
                    request.SingleTarget?.Region,
                    request.SingleTarget?.DeploymentConnection,
                    request.SingleTarget?.IsProtected,
                    request.SingleTarget?.Approvers,
                    request.SingleTarget?.Runners,
                    request.RemovedResourceBehavior,
                    request.DeletionProtectionEnabled,
                    request.Tags,
                    request.ExpectedVersion);
                var executed = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                if (executed.IsError)
                {
                    return executed.Errors.ToProblem();
                }

                var result = await mediator.Send(new GetProjectComponentQuery(projectId, componentId), cancellationToken);
                return result.Match<IResult>(value => Results.Ok(Map(value)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.UpdateProjectComponent)
            .WithSummary("Modifier un composant.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ComponentResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        components.MapPut("/{componentId:guid}/naming", async (
                Guid projectId,
                Guid componentId,
                ComponentNamingSettingsRequest request,
                IMediator mediator,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                if (request.TypeOverrides is null || request.TypeOverrides.Any(value => value is null))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["typeOverrides"] = ["La liste des gabarits par type est obligatoire et ne peut pas contenir d’élément nul."]
                    });
                }

                var command = new SetComponentNamingCommand(
                    projectId,
                    componentId,
                    request.DefaultTemplate,
                    request.TypeOverrides.Select(value => new NamingOverrideValue(value.Type, value.Template, value.Abbreviation)).ToArray(),
                    request.ExpectedVersion);
                var executed = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                if (executed.IsError)
                {
                    return executed.Errors.ToProblem();
                }

                var result = await mediator.Send(new GetProjectComponentQuery(projectId, componentId), cancellationToken);
                return result.Match<IResult>(value => Results.Ok(Map(value)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.SetComponentNaming)
            .WithSummary("Modifier les gabarits et abréviations du composant.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ComponentResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        components.MapPut("/{componentId:guid}/default-log-analytics", async (
                Guid projectId,
                Guid componentId,
                SetComponentDefaultLogAnalyticsRequest request,
                IMediator mediator,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new SetComponentDefaultLogAnalyticsCommand(
                    projectId,
                    componentId,
                    request.WorkspaceResourceId,
                    request.ExpectedVersion);
                var executed = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                if (executed.IsError)
                {
                    return executed.Errors.ToProblem();
                }

                var result = await mediator.Send(new GetProjectComponentQuery(projectId, componentId), cancellationToken);
                return result.Match<IResult>(value => Results.Ok(Map(value)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.SetComponentDefaultLogAnalytics)
            .WithSummary("Définir l'espace Log Analytics par défaut du composant.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ComponentResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        components.MapDelete("/{componentId:guid}", async (
                Guid projectId,
                Guid componentId,
                [FromBody] ExpectedVersionRequest request,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new DeleteComponentCommand(projectId, componentId, request.ExpectedVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(_ => Results.NoContent(), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.DeleteProjectComponent)
            .WithSummary("Supprimer un composant jamais publié.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        components.MapPost("/{componentId:guid}/resource-groups", async (
                Guid projectId,
                Guid componentId,
                SaveResourceGroupRequest request,
                IMediator mediator,
                IModelCommandExecutor executor,
                TimeProvider clock,
                CancellationToken cancellationToken) =>
            {
                var resourceGroupId = Guid.CreateVersion7(clock.GetUtcNow());
                var command = new CreateResourceGroupCommand(
                    projectId,
                    componentId,
                    resourceGroupId,
                    request.Name,
                    request.Region,
                    request.Tags,
                    request.ForcedNames,
                    request.ExpectedVersion);
                var executed = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                if (executed.IsError)
                {
                    return executed.Errors.ToProblem();
                }

                var result = await mediator.Send(new GetProjectComponentQuery(projectId, componentId), cancellationToken);
                return result.Match<IResult>(
                    value =>
                    {
                        var group = value.ResourceGroups.Single(candidate => candidate.Id == resourceGroupId);
                        return Results.Created(
                            $"/v1/projects/{projectId:D}/components/{componentId:D}/resource-groups/{resourceGroupId:D}",
                            Map(group));
                    },
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.CreateResourceGroup)
            .WithSummary("Créer un groupe de ressources dans un composant.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceGroupResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        components.MapPut("/{componentId:guid}/resource-groups/{resourceGroupId:guid}", async (
                Guid projectId,
                Guid componentId,
                Guid resourceGroupId,
                SaveResourceGroupRequest request,
                IMediator mediator,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateResourceGroupCommand(
                    projectId,
                    componentId,
                    resourceGroupId,
                    request.Name,
                    request.Region,
                    request.Tags,
                    request.ForcedNames,
                    request.ExpectedVersion);
                var executed = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                if (executed.IsError)
                {
                    return executed.Errors.ToProblem();
                }

                var result = await mediator.Send(new GetProjectComponentQuery(projectId, componentId), cancellationToken);
                return result.Match<IResult>(
                    value => value.ResourceGroups.SingleOrDefault(candidate => candidate.Id == resourceGroupId) is { } group
                        ? Results.Ok(Map(group))
                        : Results.NotFound(),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.UpdateResourceGroup)
            .WithSummary("Modifier un groupe de ressources.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ResourceGroupResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        components.MapDelete("/{componentId:guid}/resource-groups/{resourceGroupId:guid}", async (
                Guid projectId,
                Guid componentId,
                Guid resourceGroupId,
                [FromBody] ExpectedVersionRequest request,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new DeleteResourceGroupCommand(projectId, componentId, resourceGroupId, request.ExpectedVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(_ => Results.NoContent(), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.DeleteResourceGroup)
            .WithSummary("Supprimer un groupe de ressources vide.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return components;
    }

    private static ComponentResponse Map(ComponentSummary component) => new(
        component.Id,
        component.ProjectId,
        component.Version,
        component.Name,
        component.Code,
        component.Description,
        component.Mode,
        component.TargetsAllEnvironments,
        component.TargetEnvironmentIds,
        component.SingleTarget is { } target
            ? new ComponentSingleTargetResponse(
                target.Id,
                target.Code,
                target.SubscriptionId,
                target.Region,
                target.DeploymentConnection,
                target.IsProtected,
                target.Approvers,
                target.Runners)
            : null,
        component.RemovedResourceBehavior,
        component.DeletionProtectionEnabled,
        component.FirstPublishedAt,
        component.DefaultNameTemplate,
        component.NamingOverrides.Select(value => new NamingOverrideResponse(
            value.Type,
            value.Template,
            value.Abbreviation)).ToArray(),
        component.Tags,
        component.ResourceGroups.Select(Map).ToArray(),
        component.Resources.Select(Map).ToArray(),
        component.DefaultLogAnalyticsResourceId);

    private static ResourceGroupResponse Map(ResourceGroupSummary group) => new(
        group.Id,
        group.Version,
        group.Name,
        group.Region,
        group.Tags,
        group.ForcedNames);

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
}
