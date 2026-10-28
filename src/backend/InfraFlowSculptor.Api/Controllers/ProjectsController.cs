using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Application.Projects.Commands.AssignProjectRole;
using InfraFlowSculptor.Application.Projects.Commands.RemoveProjectRole;
using InfraFlowSculptor.Application.Projects.Commands.TakeProjectOwnership;
using InfraFlowSculptor.Application.Projects.Queries.GetEffectivePermissions;
using InfraFlowSculptor.Contracts.Projects;
using InfraFlowSculptor.Domain.Common.Features;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Api.Controllers;

public static class ProjectsController
{
    public static RouteGroupBuilder MapProjectEndpoints(this RouteGroupBuilder v1)
    {
        var projects = v1.MapGroup("/projects")
            .WithTags("Projects")
            .RequireFeature(Features.Organizations);

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
}
