using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Contracts.GitConnections;
using InfraFlowSculptor.Domain.Common.Features;
using Mediator;

namespace InfraFlowSculptor.Api.Controllers;

public static class GitConnectionsController
{
    public static RouteGroupBuilder MapGitConnectionEndpoints(this RouteGroupBuilder v1)
    {
        var connections = v1.MapGroup("/organizations/{organizationId:guid}/git-connections")
            .WithTags("Git connections")
            .RequireFeature(Features.Organizations);

        connections.MapGet("/providers", (
                Guid organizationId,
                [Microsoft.AspNetCore.Mvc.FromServices] ICurrentOrganization currentOrganization,
                [Microsoft.AspNetCore.Mvc.FromServices] GitProviderRegistry providers) =>
            currentOrganization.Id?.Value == organizationId
                ? Results.Ok(providers.Keys.Select(kind =>
                    new GitProviderAvailabilityResponse(kind, providers.Get(kind).DisplayName)).ToArray())
                : Results.NotFound())
            .WithName(EndpointNames.ListGitProviders)
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<IReadOnlyCollection<GitProviderAvailabilityResponse>>(StatusCodes.Status200OK);

        connections.MapGet("/", async (
                Guid organizationId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new ListGitConnectionsQuery(organizationId), cancellationToken);
                return result.Match<IResult>(
                    items => Results.Ok(items.Select(Map).ToArray()),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ListGitConnections)
            .WithSummary("Afficher les connexions Git de l'organisation")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<IReadOnlyCollection<GitConnectionResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        connections.MapPost("/", async (
                Guid organizationId,
                CreateGitConnectionRequest request,
                IMediator mediator,
                TimeProvider clock,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new CreateGitConnectionCommand(
                    organizationId,
                    Guid.CreateVersion7(clock.GetUtcNow()),
                    request.ProviderKind,
                    request.Name,
                    request.Organization,
                    request.TenantId,
                    request.AuthenticationMode,
                    request.PersonalAccessTokenExpiresAt,
                    request.PersonalAccessToken), cancellationToken);
                return result.Match<IResult>(
                    connection => Results.Created(
                        $"/v1/organizations/{organizationId:D}/git-connections/{connection.Id:D}",
                        Map(connection)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.CreateGitConnection)
            .WithSummary("Tester et ajouter une connexion Git")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<GitConnectionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        connections.MapPost("/{connectionId:guid}/test", async (
                Guid organizationId,
                Guid connectionId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new TestGitConnectionCommand(organizationId, connectionId),
                    cancellationToken);
                return result.Match<IResult>(
                    connection => Results.Ok(Map(connection)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.TestGitConnection)
            .WithSummary("Tester une connexion Git et lire ses dépôts")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<GitConnectionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        connections.MapPut("/{connectionId:guid}", async (
                Guid organizationId,
                Guid connectionId,
                UpdateGitConnectionRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new UpdateGitConnectionCommand(
                    organizationId,
                    connectionId,
                    request.ExpectedVersion,
                    request.Name,
                    request.Organization,
                    request.TenantId,
                    request.PersonalAccessTokenExpiresAt,
                    request.PersonalAccessToken), cancellationToken);
                return result.Match<IResult>(
                    connection => Results.Ok(Map(connection)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.UpdateGitConnection)
            .WithSummary("Tester et modifier une connexion Git")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<GitConnectionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        connections.MapPut("/{connectionId:guid}/repositories/access", async (
                Guid organizationId,
                Guid connectionId,
                SetGitConnectionRepositoryAccessRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new SetGitConnectionRepositoryAccessCommand(
                    organizationId,
                    connectionId,
                    request.ExpectedVersion,
                    request.ProviderRepositoryId,
                    request.OpenToAllProjects,
                    request.ProjectIds), cancellationToken);
                return result.Match<IResult>(
                    connection => Results.Ok(Map(connection)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.SetGitConnectionRepositoryAccess)
            .WithSummary("Définir les projets autorisés pour un dépôt")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<GitConnectionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        connections.MapDelete("/{connectionId:guid}", async (
                Guid organizationId,
                Guid connectionId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new DeleteGitConnectionCommand(organizationId, connectionId),
                    cancellationToken);
                return result.Match<IResult>(
                    _ => Results.NoContent(),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.DeleteGitConnection)
            .WithSummary("Retirer une connexion Git inutilisée")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return v1;
    }

    private static GitConnectionResponse Map(GitConnectionSummary connection) => new(
        connection.Id,
        connection.ProviderKind,
        connection.Name,
        connection.Organization,
        connection.TenantId,
        connection.AuthenticationMode,
        connection.PersonalAccessTokenExpiresAt,
        connection.Version,
        connection.Repositories.Select(repository => new GitConnectionRepositoryResponse(
            repository.ProviderRepositoryId,
            repository.Name,
            repository.DefaultBranch,
            repository.OpenToAllProjects,
            repository.AllowedProjects.Select(projectId => projectId.Value).ToArray())).ToArray(),
        connection.UsedByProjectNames);
}
