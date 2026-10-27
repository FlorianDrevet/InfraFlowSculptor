using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Application.Projects.Commands.AddProjectEnvironment;
using InfraFlowSculptor.Application.Projects.Commands.AssignProjectRole;
using InfraFlowSculptor.Application.Projects.Commands.CreateProject;
using InfraFlowSculptor.Application.Projects.Commands.DeleteProject;
using InfraFlowSculptor.Application.Projects.Commands.DeleteProjectEnvironment;
using InfraFlowSculptor.Application.Projects.Commands.MoveProjectEnvironment;
using InfraFlowSculptor.Application.Projects.Commands.RemoveProjectRole;
using InfraFlowSculptor.Application.Projects.Commands.RestoreProject;
using InfraFlowSculptor.Application.Projects.Commands.SetProjectTags;
using InfraFlowSculptor.Application.Projects.Commands.TakeProjectOwnership;
using InfraFlowSculptor.Application.Projects.Commands.UpdateProjectEnvironment;
using InfraFlowSculptor.Application.Projects.Queries.GetEffectivePermissions;
using InfraFlowSculptor.Application.Projects.Queries.GetProject;
using InfraFlowSculptor.Application.Projects.Queries.ListProjects;
using InfraFlowSculptor.Application.Projects.Queries.ListProjectEnvironments;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Contracts.Projects;
using InfraFlowSculptor.Domain.Common.Features;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace InfraFlowSculptor.Api.Controllers;

public static class ProjectsController
{
    public static RouteGroupBuilder MapProjectEndpoints(this RouteGroupBuilder v1)
    {
        var projects = v1.MapGroup("/projects")
            .WithTags("Projects")
            .RequireFeature(Features.Organizations)
            .RequireFeature(Features.ProjectSetup);

        projects.MapGet("", async (IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new ListProjectsQuery(), cancellationToken);
                return result.Match<IResult>(
                    values => Results.Ok(values.Select(Map).ToArray()),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ListProjects)
            .WithSummary("Lister les projets visibles dans l’organisation active.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<IReadOnlyList<ProjectSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapPost("", async (
                CreateProjectRequest request,
                IMediator mediator,
                IModelCommandExecutor executor,
                ICatalogProvider catalogProvider,
                TimeProvider clock,
                CancellationToken cancellationToken) =>
            {
                var catalogVersion = string.IsNullOrWhiteSpace(request.CatalogVersion)
                    ? catalogProvider.LatestPublished
                    : catalogProvider.GetVersion(request.CatalogVersion);
                if (catalogVersion is null || !string.Equals(catalogVersion.Status, "published", StringComparison.Ordinal))
                {
                    return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "No published catalog version is available.");
                }

                var projectId = Guid.CreateVersion7(clock.GetUtcNow());
                var command = new CreateProjectCommand(
                    projectId,
                    request.TenantId,
                    request.Name,
                    request.Code,
                    request.Description,
                    catalogVersion.Version,
                    request.CoordinatorUserProfileId,
                    request.DefaultRunners ?? [],
                    request.DefaultReviewers ?? []);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(
                    _ => Results.Created(
                        $"/v1/projects/{projectId:D}",
                        new ProjectSummaryResponse(
                            projectId,
                            1,
                            request.Name.Trim(),
                            request.Code.Trim(),
                            request.Description.Trim(),
                            1,
                            0,
                            null,
                            null,
                            request.TenantId,
                            "Bicep",
                            "AzureDevOps",
                            "AVM registre public",
                            catalogVersion.Version,
                            true,
                            request.CoordinatorUserProfileId,
                            request.DefaultRunners ?? [],
                            request.DefaultReviewers ?? [],
                            new Dictionary<string, string>())),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.CreateProject)
            .WithSummary("Créer un projet et l’attribuer à son créateur.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ProjectSummaryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetProjectQuery(id), cancellationToken);
                return result.Match<IResult>(value => Results.Ok(Map(value)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetProject)
            .WithSummary("Consulter un projet.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<ProjectSummaryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapPut("/{id:guid}/tags", async (
                Guid id,
                SetProjectTagsRequest request,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new SetProjectTagsCommand(id, request.Tags, request.ExpectedVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(
                    _ => Results.NoContent(),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.SetProjectTags)
            .WithSummary("Remplacer les tags déclarés sur le projet.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapDelete("/{id:guid}", async (
                Guid id,
                [FromBody] DeleteProjectRequest request,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new DeleteProjectCommand(id, request.NameConfirmation, request.ExpectedVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(_ => Results.NoContent(), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.DeleteProject)
            .WithSummary("Supprimer un projet de façon réversible pendant 30 jours.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapPost("/{id:guid}/restore", async (
                Guid id,
                RestoreProjectRequest request,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new RestoreProjectCommand(id, request.ExpectedVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(_ => Results.NoContent(), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.RestoreProject)
            .WithSummary("Restaurer un projet supprimé depuis moins de 30 jours.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var environments = projects.MapGroup("/{id:guid}/environments")
            .WithTags("Project environments")
            .RequireFeature(Features.ProjectEnvironments);

        environments.MapGet("", async (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new ListProjectEnvironmentsQuery(id), cancellationToken);
                return result.Match<IResult>(
                    values => Results.Ok(values.Select(Map).ToArray()),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ListProjectEnvironments)
            .WithSummary("Lister les environnements dans leur ordre de promotion.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<IReadOnlyList<ProjectEnvironmentResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        environments.MapPost("", async (
                Guid id,
                AddProjectEnvironmentRequest request,
                IMediator mediator,
                IModelCommandExecutor executor,
                TimeProvider clock,
                CancellationToken cancellationToken) =>
            {
                var environmentId = Guid.CreateVersion7(clock.GetUtcNow());
                var command = new AddProjectEnvironmentCommand(
                    id,
                    environmentId,
                    request.Name,
                    request.Code,
                    request.Position,
                    request.SubscriptionId,
                    request.Region,
                    request.IsProtected,
                    request.Approvers,
                    request.Runners,
                    request.Description,
                    request.Tags,
                    request.ExpectedVersion,
                    request.DeploymentConnection);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                if (result.IsError)
                {
                    return result.Errors.ToProblem();
                }

                var environmentsResult = await mediator.Send(new ListProjectEnvironmentsQuery(id), cancellationToken);
                if (environmentsResult.IsError)
                {
                    return environmentsResult.Errors.ToProblem();
                }

                var environment = environmentsResult.Value.Single(value => value.Id == environmentId);
                return Results.Created($"/v1/projects/{id:D}/environments/{environmentId:D}", Map(environment));
            })
            .WithName(EndpointNames.AddProjectEnvironment)
            .WithSummary("Ajouter un environnement à une position de la chaîne.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ProjectEnvironmentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        environments.MapPut("/{environmentId:guid}", async (
                Guid id,
                Guid environmentId,
                UpdateProjectEnvironmentRequest request,
                IMediator mediator,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateProjectEnvironmentCommand(
                    id,
                    environmentId,
                    request.Name,
                    request.Code,
                    request.SubscriptionId,
                    request.Region,
                    request.IsProtected,
                    request.Approvers,
                    request.Runners,
                    request.Description,
                    request.Tags,
                    request.ConfirmTargetImpact,
                    request.ExpectedVersion,
                    request.DeploymentConnection);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                if (result.IsError)
                {
                    return result.Errors.ToProblem();
                }

                var environmentsResult = await mediator.Send(new ListProjectEnvironmentsQuery(id), cancellationToken);
                return environmentsResult.Match<IResult>(
                    values => Results.Ok(Map(values.Single(value => value.Id == environmentId))),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.UpdateProjectEnvironment)
            .WithSummary("Modifier un environnement et confirmer l’impact d’un changement de cible publiée.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ProjectEnvironmentResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        environments.MapPatch("/{environmentId:guid}/position", async (
                Guid id,
                Guid environmentId,
                MoveProjectEnvironmentRequest request,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new MoveProjectEnvironmentCommand(id, environmentId, request.Position, request.ExpectedVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(_ => Results.NoContent(), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.MoveProjectEnvironment)
            .WithSummary("Déplacer un environnement dans la chaîne de promotion.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        environments.MapDelete("/{environmentId:guid}", async (
                Guid id,
                Guid environmentId,
                [FromBody] DeleteProjectEnvironmentRequest request,
                IModelCommandExecutor executor,
                CancellationToken cancellationToken) =>
            {
                var command = new DeleteProjectEnvironmentCommand(id, environmentId, request.ConfirmDeletion, request.ExpectedVersion);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(_ => Results.NoContent(), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.DeleteProjectEnvironment)
            .WithSummary("Supprimer un environnement après confirmation des données impactées.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapGet("/{id:guid}/permissions/me", async (
                Guid id,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetEffectivePermissionsQuery(id, MemberId: null), cancellationToken);
                return result.Match<IResult>(
                    permissions => Results.Ok(Map(permissions)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetMyProjectPermissions)
            .WithSummary("Afficher ses droits effectifs sur un projet")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<EffectiveProjectPermissionsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapGet("/{id:guid}/members/{memberId:guid}/permissions", async (
                Guid id,
                Guid memberId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetEffectivePermissionsQuery(id, memberId), cancellationToken);
                return result.Match<IResult>(
                    permissions => Results.Ok(Map(permissions)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetProjectMemberPermissions)
            .WithSummary("Afficher les droits effectifs d’un membre sur un projet")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<EffectiveProjectPermissionsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapPost("/{id:guid}/members/{memberId:guid}/roles", async (
                Guid id,
                Guid memberId,
                AssignProjectRoleRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new AssignProjectRoleCommand(id, memberId, request.Role, request.Scope, request.ExpectedVersion),
                    cancellationToken);
                return result.Match<IResult>(
                    assignment => Results.Ok(Map(assignment)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.AssignProjectRole)
            .WithSummary("Attribuer un rôle de projet (UC-ORG-08, RG-ORG-23)")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ProjectRoleAssignmentResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapDelete("/{id:guid}/members/{memberId:guid}/roles/{role}", async (
                Guid id,
                Guid memberId,
                ProjectRole role,
                ProjectRoleScope scope,
                int expectedVersion,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new RemoveProjectRoleCommand(id, memberId, role, scope, expectedVersion),
                    cancellationToken);
                return result.Match<IResult>(
                    _ => Results.NoContent(),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.RemoveProjectRole)
            .WithSummary("Retirer un rôle de projet (UC-ORG-09, RG-ORG-11)")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projects.MapPost("/{id:guid}/ownership", async (
                Guid id,
                TakeProjectOwnershipRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new TakeProjectOwnershipCommand(id, request.ExpectedVersion),
                    cancellationToken);
                return result.Match<IResult>(
                    assignment => Results.Ok(Map(assignment)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.TakeProjectOwnership)
            .WithSummary("Prendre la propriété d’un projet en tant qu’administrateur d’organisation")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ProjectRoleAssignmentResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return projects;
    }

    private static EffectiveProjectPermissionsResponse Map(ProjectEffectivePermissions permissions) =>
        new(
            permissions.MemberId,
            permissions.Version,
            permissions.Permissions,
            permissions.Assignments.Select(Map).ToArray());

    private static ProjectRoleAssignmentResponse Map(ProjectRoleAssignmentSummary assignment) =>
        new(assignment.MemberId, assignment.Role, assignment.Scope, assignment.Version, assignment.Source);

    private static ProjectSummaryResponse Map(ProjectSummary project) =>
        new(
            project.Id,
            project.Version,
            project.Name,
            project.Code,
            project.Description,
            project.ModelVersion,
            project.EnvironmentCount,
            project.FirstPublishedAt,
            project.DeletedAt,
            project.TenantId,
            project.InfrastructureLanguage,
            project.CiPlatform,
            project.ModuleSource,
            project.CatalogVersion,
            project.SystemTagsEnabled,
            project.CoordinatorUserProfileId,
            project.DefaultRunners,
            project.DefaultReviewers,
            project.Tags);

    private static ProjectEnvironmentResponse Map(ProjectEnvironmentSummary environment) =>
        new(
            environment.Id,
            environment.Name,
            environment.Code,
            environment.Position,
            environment.SubscriptionId,
            environment.Region,
            environment.DeploymentConnection,
            environment.IsProtected,
            environment.Approvers,
            environment.Runners,
            environment.Description,
            environment.Tags,
            environment.FirstPublishedAt);
}
