using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Publications;
using InfraFlowSculptor.Contracts.Publications;
using InfraFlowSculptor.Domain.Common.Features;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace InfraFlowSculptor.Api.Controllers;

public static class PublicationsController
{
    public static RouteGroupBuilder MapPublicationEndpoints(this RouteGroupBuilder projects)
    {
        var revisionPublications = projects.MapGroup("/{projectId:guid}/revisions")
            .WithTags("Publications")
            .RequireFeature(Features.Revisions);
        var publications = projects.MapGroup("/{projectId:guid}/publications")
            .WithTags("Publications")
            .RequireFeature(Features.Revisions);

        revisionPublications.MapPost("/{revisionId:guid}/publications", async (
                Guid projectId,
                Guid revisionId,
                StartRevisionPublicationRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new StartRevisionPublicationCommand(
                    projectId,
                    revisionId,
                    request.Message,
                    request.AcceptStaleRevision,
                    request.DestinationIds), cancellationToken);
                return result.Match<IResult>(value => Results.Accepted(
                    $"/v1/projects/{projectId:D}/publications/{value.Id:D}",
                    Map(value)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.StartRevisionPublication)
            .WithSummary("Préparer la publication d’une révision dans ses dépôts.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Publish)
            .WithIdempotency()
            .Produces<PublicationResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        publications.MapGet("", async (
                Guid projectId,
                int? limit,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new ListProjectPublicationsQuery(projectId, limit ?? 30), cancellationToken);
                return result.Match<IResult>(
                    values => Results.Ok(values.Select(Map).ToArray()),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ListProjectPublications)
            .WithSummary("Lister les publications d’un projet.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<IReadOnlyCollection<PublicationResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        publications.MapGet("/{publicationId:guid}", async (
                Guid projectId,
                Guid publicationId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetPublicationQuery(projectId, publicationId), cancellationToken);
                return result.Match<IResult>(value => Results.Ok(Map(value)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetPublication)
            .WithSummary("Consulter l’état d’une publication et ses changements par dépôt.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<PublicationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        publications.MapPost("/{publicationId:guid}/targets/{targetId:guid}/confirm-ownership", async (
                Guid projectId,
                Guid publicationId,
                Guid targetId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new ConfirmPublicationOwnershipCommand(projectId, publicationId, targetId),
                    cancellationToken);
                return result.Match<IResult>(value => Results.Ok(Map(value)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ConfirmPublicationOwnership)
            .WithSummary("Confirmer la prise en charge des fichiers en conflit pour un dépôt.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Publish)
            .WithIdempotency()
            .Produces<PublicationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        publications.MapPost("/{publicationId:guid}/publish", async (
                Guid projectId,
                Guid publicationId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new StartPublicationWriteCommand(projectId, publicationId), cancellationToken);
                return result.Match<IResult>(value => Results.Accepted(
                    $"/v1/projects/{projectId:D}/publications/{publicationId:D}",
                    Map(value)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.StartPublicationWrite)
            .WithSummary("Publier les dépôts préparés dans leurs branches de pull request.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Publish)
            .WithIdempotency()
            .Produces<PublicationResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return publications;
    }

    private static PublicationResponse Map(PublicationView view) => new(
        view.Id,
        view.ProjectId,
        view.RevisionId,
        view.RevisionNumber,
        view.Message,
        view.State,
        view.RequestedAt,
        view.PreparedAt,
        view.CompletedAt,
        view.Targets.Select(target => new PublicationTargetResponse(
            target.Id,
            target.GitConnectionId,
            target.ProviderRepositoryId,
            target.RepositoryName,
            target.TargetBranch,
            target.SourceBranch,
            target.State,
            target.RequiresConfirmation,
            target.OwnershipConfirmed,
            target.FailureCode,
            target.CommitSha,
            target.PullRequestId,
            target.PullRequestUrl,
            target.PullRequestState,
            target.MergeCommitSha,
            target.Destinations.Select(destination => new PublicationTargetDestinationResponse(
                destination.Id,
                destination.BasePath)).ToArray(),
            target.Files.Select(file => new PublicationTargetFileResponse(
                file.DestinationId,
                file.Path,
                file.Action,
                file.Sha256,
                file.ExistingSha256)).ToArray())).ToArray());
}
