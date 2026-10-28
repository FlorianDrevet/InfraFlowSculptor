using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.Idempotency;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Naming;
using InfraFlowSculptor.Application.Projects.Commands.SetProjectNaming;
using InfraFlowSculptor.Application.Projects.Queries.GetProjectNamingSettings;
using InfraFlowSculptor.Application.Projects.Queries.PreviewProjectNaming;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Contracts.Naming;
using InfraFlowSculptor.Domain.Common.Features;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;
using Mediator;

namespace InfraFlowSculptor.Api.Controllers;

public static class NamingController
{
    public static RouteGroupBuilder MapNamingEndpoints(this RouteGroupBuilder v1)
    {
        var naming = v1.MapGroup("/naming")
            .WithTags("Naming")
            .RequireFeature(Features.Organizations)
            .RequireFeature(Features.ProjectSetup);

        naming.MapPost("/preview", (NamingPreviewRequest request, ICatalogProvider catalogProvider) =>
            {
                if (string.IsNullOrWhiteSpace(request.ProjectName)
                    || string.IsNullOrWhiteSpace(request.ProjectCode)
                    || string.IsNullOrWhiteSpace(request.NamingPreset)
                    || string.IsNullOrWhiteSpace(request.DefaultNameTemplate)
                    || string.IsNullOrWhiteSpace(request.ResourceGroupNameTemplate)
                    || request.Environments is null
                    || request.Environments.Count == 0)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["request"] = ["Project identity, both templates, and at least one environment are required."]
                    });
                }

                var catalog = catalogProvider.LatestPublished;
                if (catalog is null)
                {
                    return Results.Problem(
                        "A published catalog is required to preview resource names.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }

                var environments = request.Environments.Select(environment =>
                    new EnvironmentSnapshot(environment.Code, environment.Region)
                    {
                        Id = Guid.NewGuid(),
                        Name = environment.Code
                    }).ToArray();
                var targetIds = environments.Select(environment => environment.Id).ToArray();
                var groupId = Guid.NewGuid();
                var sampleResource = new ResourceSnapshot(
                    Guid.NewGuid(),
                    groupId,
                    "KeyVault",
                    "main",
                    string.Empty,
                    false,
                    new Dictionary<string, System.Text.Json.JsonElement>(),
                    new Dictionary<Guid, IReadOnlyDictionary<string, System.Text.Json.JsonElement>>(),
                    new Dictionary<Guid, bool>(),
                    new Dictionary<Guid, string>(),
                    new Dictionary<Guid, string>(),
                    new Dictionary<string, string>(),
                    []);
                var component = new ComponentSnapshot(
                    Guid.NewGuid(),
                    "Main",
                    "main",
                    "PerEnvironment",
                    targetIds,
                    null,
                    [new ResourceGroupSnapshot(groupId, "main", null, new Dictionary<string, string>(), new Dictionary<Guid, string>())],
                    [sampleResource])
                {
                    TargetsAllEnvironments = true
                };
                var snapshot = new ModelSnapshot(
                    new ProjectSnapshot(request.ProjectName, request.ProjectCode, string.Empty),
                    environments,
                    new NamingConventionSnapshot(
                        request.NamingPreset,
                        request.DefaultNameTemplate,
                        request.ResourceGroupNameTemplate),
                    request.Tags ?? new Dictionary<string, string>(),
                    [component])
                {
                    NamingRules = CatalogNamingSnapshotMapper.CreateRules(catalog),
                    RegionCodes = CatalogNamingSnapshotMapper.CreateRegionCodes(catalog)
                };
                var preview = NamingEngine.Preview(snapshot);
                return Results.Ok(new NamingPreviewResponse(
                    preview.Items.Select(item => new NamingPreviewItemResponse(
                        item.EnvironmentCode,
                        item.ResourceName,
                        item.ResourceGroupName)).ToArray(),
                    preview.Issues.Select(issue => new NamingPreviewIssueResponse(issue.Code, issue.Message)).ToArray()));
            })
            .WithName(EndpointNames.PreviewNaming)
            .WithSummary("Calculer l’aperçu des noms d’un projet non enregistré.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<NamingPreviewResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var projectNaming = v1.MapGroup("/projects/{projectId:guid}/naming")
            .WithTags("Naming")
            .RequireFeature(Features.Organizations)
            .RequireFeature(Features.ProjectSetup);

        projectNaming.MapGet("", async (
                Guid projectId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetProjectNamingSettingsQuery(projectId), cancellationToken);
                return result.Match<IResult>(settings => Results.Ok(MapSettings(settings)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.GetProjectNaming)
            .WithSummary("Consulter les gabarits et abréviations du projet.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<ProjectNamingSettingsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        projectNaming.MapPut("", async (
                Guid projectId,
                ProjectNamingSettingsRequest request,
                IModelCommandExecutor executor,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                if (request.TypeOverrides is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["typeOverrides"] = ["La liste des gabarits par type est obligatoire."]
                    });
                }

                var command = new SetProjectNamingCommand(
                    projectId,
                    request.Preset,
                    request.DefaultTemplate,
                    request.ResourceGroupTemplate,
                    request.TypeOverrides.Select(value => new NamingOverrideValue(value.Type, value.Template, value.Abbreviation)).ToArray(),
                    request.ExpectedVersion);
                var executed = await executor.ExecuteAsync([command], ModelCommandMode.Apply, cancellationToken);
                if (executed.IsError)
                {
                    return executed.Errors.ToProblem();
                }

                var updated = await mediator.Send(new GetProjectNamingSettingsQuery(projectId), cancellationToken);
                return updated.Match<IResult>(settings => Results.Ok(MapSettings(settings)), errors => errors.ToProblem());
            })
            .WithName(EndpointNames.SetProjectNaming)
            .WithSummary("Modifier les gabarits et abréviations du projet.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithIdempotency()
            .Produces<ProjectNamingSettingsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        projectNaming.MapPost("/preview", async (
                Guid projectId,
                ProjectNamingPreviewRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                if (request.TypeOverrides is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["typeOverrides"] = ["La liste des gabarits par type est obligatoire."]
                    });
                }

                var result = await mediator.Send(new PreviewProjectNamingQuery(
                    projectId,
                    request.Preset,
                    request.DefaultTemplate,
                    request.ResourceGroupTemplate,
                    request.TypeOverrides.Select(value => new NamingOverrideValue(value.Type, value.Template, value.Abbreviation)).ToArray()), cancellationToken);
                return result.Match<IResult>(
                    preview => Results.Ok(MapPreview(preview)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.PreviewProjectNaming)
            .WithSummary("Calculer les noms de toutes les ressources du projet.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<ProjectNamingPreviewResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return v1;
    }

    private static ProjectNamingSettingsResponse MapSettings(
        InfraFlowSculptor.Application.Projects.ProjectNamingSettings settings) => new(
        settings.ProjectId,
        settings.Version,
        settings.Preset,
        settings.DefaultTemplate,
        settings.ResourceGroupTemplate,
        settings.TypeOverrides.Select(value => new NamingOverrideResponse(value.Type, value.Template, value.Abbreviation)).ToArray(),
        settings.IsPublished);

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

    private static NamingExplanationResponse? MapExplanation(NamingExplanation? explanation) => explanation is null
        ? null
        : new NamingExplanationResponse(
            explanation.Level,
            explanation.Template,
            explanation.RawName,
            explanation.FinalName,
            explanation.Transformations);
}
