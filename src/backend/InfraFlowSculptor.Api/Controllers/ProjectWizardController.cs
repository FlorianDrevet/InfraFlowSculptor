using System.Text.Json;
using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Projects.Wizard;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Contracts.Projects;
using InfraFlowSculptor.Domain.Common.Features;
using Mediator;

namespace InfraFlowSculptor.Api.Controllers;

public static class ProjectWizardController
{
    public static RouteGroupBuilder MapProjectWizardEndpoints(this RouteGroupBuilder v1)
    {
        var projects = v1.MapGroup("/projects")
            .WithTags("Projects")
            .RequireFeature(Features.Organizations)
            .RequireFeature(Features.ProjectSetup);

        projects.MapGet("/wizard-draft", async (IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetWizardDraftQuery(), cancellationToken);
                return result.Match<IResult>(
                    draft => draft is null
                        ? Results.NoContent()
                        : Results.Ok(new ProjectWizardDraftResponse(ParseContent(draft.ContentJson), draft.ExpiresAt)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetWizardDraft)
            .WithSummary("Reprendre le brouillon courant de l’assistant de création.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<ProjectWizardDraftResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        projects.MapPut("/wizard-draft", async (
                SaveWizardDraftRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var content = request.Content.ValueKind == JsonValueKind.Undefined
                    ? string.Empty
                    : request.Content.GetRawText();
                var result = await mediator.Send(new SaveWizardDraftCommand(content), cancellationToken);
                return result.Match<IResult>(
                    draft => Results.Ok(new ProjectWizardDraftResponse(ParseContent(draft.ContentJson), draft.ExpiresAt)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.SaveWizardDraft)
            .WithSummary("Enregistrer la progression de l’assistant pour l’utilisateur et l’organisation actifs.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .Produces<ProjectWizardDraftResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        projects.MapDelete("/wizard-draft", async (IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new DiscardWizardDraftCommand(), cancellationToken);
                return result.Match<IResult>(
                    _ => Results.NoContent(),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.DiscardWizardDraft)
            .WithSummary("Supprimer le brouillon de l’assistant de création.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        projects.MapPost("/from-wizard", async (
                CreateProjectFromWizardRequest request,
                IModelCommandExecutor executor,
                ICatalogProvider catalogProvider,
                TimeProvider clock,
                CancellationToken cancellationToken) =>
            {
                var catalog = string.IsNullOrWhiteSpace(request.CatalogVersion)
                    ? catalogProvider.LatestPublished
                    : catalogProvider.GetVersion(request.CatalogVersion);
                if (catalog is null || !string.Equals(catalog.Status, "published", StringComparison.Ordinal))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        title: "No published catalog version is available.");
                }

                var projectId = Guid.CreateVersion7(clock.GetUtcNow());
                var environments = request.Environments.Select((environment, index) => new CreateProjectFromWizardEnvironment(
                    Guid.CreateVersion7(clock.GetUtcNow().AddTicks(index + 1)),
                    environment.Name,
                    environment.Code,
                    environment.SubscriptionId,
                    environment.Region,
                    environment.IsProtected,
                    environment.Approvers,
                    environment.Description)).ToArray();
                var command = new CreateProjectFromWizardCommand(
                    projectId,
                    request.TenantId,
                    request.Name,
                    request.Code,
                    request.Description,
                    catalog.Version,
                    request.NamingPreset,
                    request.DefaultNameTemplate,
                    request.ResourceGroupNameTemplate,
                    environments,
                    request.Tags ?? new Dictionary<string, string>(),
                    request.PublishingPreset);
                var result = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                return result.Match<IResult>(
                    _ => Results.Created(
                        $"/v1/projects/{projectId:D}",
                        new CreateProjectFromWizardResponse(projectId, request.Name.Trim(), request.Code.Trim())),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.CreateProjectFromWizard)
            .WithSummary("Créer atomiquement un projet et les choix de son assistant.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<CreateProjectFromWizardResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return v1;
    }

    private static JsonElement ParseContent(string contentJson)
    {
        using var document = JsonDocument.Parse(contentJson);
        return document.RootElement.Clone();
    }
}
