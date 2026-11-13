using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Contracts.Revisions;
using InfraFlowSculptor.Domain.Common.Features;
using InfraFlowSculptor.Domain.RevisionAggregate;
using InfraFlowSculptor.Engine.Plan;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace InfraFlowSculptor.Api.Controllers;

public static class RevisionsController
{
    public static RouteGroupBuilder MapRevisionEndpoints(this RouteGroupBuilder projects)
    {
        var revisions = projects.MapGroup("/{projectId:guid}/revisions")
            .WithTags("Revisions")
            .RequireFeature(Features.Revisions);

        revisions.MapPost("", async (
                Guid projectId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GenerateRevisionCommand(projectId), cancellationToken);
                return result.Match<IResult>(value =>
                {
                    var response = new RevisionGenerationResponse(
                        value.RevisionId,
                        value.Number,
                        value.State.ToString(),
                        value.QueuePosition,
                        value.IsUnchanged);
                    return value.IsUnchanged
                        ? Results.Ok(response)
                        : Results.Accepted($"/v1/projects/{projectId:D}/revisions/{value.RevisionId:D}", response);
                }, errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GenerateRevision)
            .WithSummary("Demander la génération d’une révision immuable.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Generate)
            .WithIdempotency()
            .Produces<RevisionGenerationResponse>(StatusCodes.Status200OK)
            .Produces<RevisionGenerationResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        revisions.MapGet("", async (
                Guid projectId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new ListProjectRevisionsQuery(projectId), cancellationToken);
                return result.Match<IResult>(
                    values => Results.Ok(new RevisionListResponse(
                        values.Items.Select(MapSummary).ToArray(),
                        values.NextNumber)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ListRevisions)
            .WithSummary("Lister les révisions d’un projet.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<RevisionListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        revisions.MapGet("/{revisionId:guid}", async (
                Guid projectId,
                Guid revisionId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetProjectRevisionQuery(projectId, revisionId), cancellationToken);
                return result.Match<IResult>(
                    value => Results.Ok(MapDetail(value)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetRevision)
            .WithSummary("Consulter les métadonnées d’une révision.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<RevisionDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        revisions.MapGet("/{revisionId:guid}/plan", async (
                Guid projectId,
                Guid revisionId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetProjectRevisionQuery(projectId, revisionId), cancellationToken);
                return result.Match<IResult>(value =>
                {
                    if (value.Revision.State != RevisionState.Generated)
                    {
                        return Results.Conflict(new ProblemDetails
                        {
                            Status = StatusCodes.Status409Conflict,
                            Title = "La révision n’est pas encore générée."
                        });
                    }

                    return Results.Ok(DeploymentPlanJsonSerializer.Deserialize(value.Revision.DeploymentPlanJson));
                }, errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetRevisionPlan)
            .WithSummary("Consulter le plan figé d’une révision générée.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<DeploymentPlan>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        revisions.MapGet("/{revisionId:guid}/files", async (
                Guid projectId,
                Guid revisionId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetProjectRevisionQuery(projectId, revisionId), cancellationToken);
                return result.Match<IResult>(value =>
                {
                    if (value.Revision.State != RevisionState.Generated)
                    {
                        return Results.Conflict(new ProblemDetails
                        {
                            Status = StatusCodes.Status409Conflict,
                            Title = "Les fichiers ne sont pas encore disponibles."
                        });
                    }

                    return Results.Ok(value.Revision.Files
                        .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                        .Select(file => new RevisionFileResponse(file.RelativePath, file.Sha256, file.Size))
                        .ToArray());
                }, errors => errors.ToProblem());
            })
            .WithName(EndpointNames.ListRevisionFiles)
            .WithSummary("Lister les fichiers d’une révision générée.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<IReadOnlyList<RevisionFileResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        revisions.MapGet("/{revisionId:guid}/files/{**path}", async (
                Guid projectId,
                Guid revisionId,
                string path,
                IMediator mediator,
                [FromServices] IRevisionBlobStore blobStore,
                CancellationToken cancellationToken,
                [FromQuery] bool download = false) =>
            {
                var result = await mediator.Send(new GetProjectRevisionQuery(projectId, revisionId), cancellationToken);
                if (result.IsError)
                {
                    return result.Errors.ToProblem();
                }

                var view = result.Value;
                if (view.Revision.State != RevisionState.Generated)
                {
                    return Results.Conflict(new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "Les fichiers ne sont pas encore disponibles."
                    });
                }

                var normalizedPath = string.Join('/', path.Split('/').Select(Uri.UnescapeDataString));
                var file = view.Revision.Files.SingleOrDefault(candidate =>
                    string.Equals(candidate.RelativePath, normalizedPath, StringComparison.Ordinal));
                if (file is null)
                {
                    return Results.NotFound();
                }

                var content = await blobStore.OpenFileAsync(file.Sha256, cancellationToken);
                return Results.File(
                    content,
                    GetContentType(file.RelativePath),
                    download ? Path.GetFileName(file.RelativePath) : null);
            })
            .WithName(EndpointNames.GetRevisionFile)
            .WithSummary("Consulter ou télécharger un fichier d’une révision.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        revisions.MapGet("/{revisionId:guid}/archive", async (
                Guid projectId,
                Guid revisionId,
                IMediator mediator,
                [FromServices] IRevisionBlobStore blobStore,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetProjectRevisionQuery(projectId, revisionId), cancellationToken);
                if (result.IsError)
                {
                    return result.Errors.ToProblem();
                }

                var view = result.Value;
                if (view.Revision.State != RevisionState.Generated)
                {
                    return Results.Conflict(new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "L’archive n’est pas encore disponible."
                    });
                }

                var archive = await blobStore.GetOrCreateArchiveAsync(
                    view.Revision.Id.Value,
                    view.Revision.Files,
                    cancellationToken);
                return Results.File(archive, "application/zip", $"revision-{view.Revision.Number}.zip");
            })
            .WithName(EndpointNames.GetRevisionArchive)
            .WithSummary("Télécharger l’archive d’une révision générée.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return revisions;
    }

    private static RevisionDetailResponse MapDetail(RevisionView view) => new(
        MapSummary(view),
        view.Revision.Findings.Select(finding => new RevisionFindingResponse(
            finding.Code,
            finding.Severity.ToString(),
            finding.MessageFr,
            finding.MessageEn,
            finding.SubjectType,
            finding.SubjectId,
            finding.EnvironmentId)).ToArray(),
        view.Revision.Files
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .Select(file => new RevisionFileResponse(file.RelativePath, file.Sha256, file.Size))
            .ToArray(),
        view.Revision.Changes.Select(change => new RevisionChangeResponse(
            change.Category,
            change.Subject,
            change.DescriptionFr,
            change.DescriptionEn)).ToArray());

    private static RevisionSummaryResponse MapSummary(RevisionView view) => new(
        view.Revision.Id.Value,
        view.Revision.Number,
        view.Revision.State.ToString(),
        view.Revision.AuthorTenantId,
        view.Revision.AuthorObjectId,
        view.Revision.ModelVersion,
        view.Revision.CatalogVersion,
        view.Revision.InfrastructureLanguage,
        view.Revision.CiPlatform,
        view.Revision.InputSha256,
        view.Revision.CreatedAt,
        view.Revision.StartedAt,
        view.Revision.CompletedAt,
        view.Revision.FailureCode,
        view.IsStale,
        view.QueuePosition,
        view.Revision.Findings.Count,
        view.Revision.Files.Count,
        view.Revision.Changes.Count);

    private static string GetContentType(string relativePath) =>
        Path.GetExtension(relativePath).ToLowerInvariant() switch
        {
            ".json" => "application/json; charset=utf-8",
            ".yml" or ".yaml" => "application/yaml; charset=utf-8",
            ".zip" => "application/zip",
            ".ps1" or ".psm1" or ".bicep" or ".md" or ".hcl" => "text/plain; charset=utf-8",
            _ => "application/octet-stream"
        };
}
