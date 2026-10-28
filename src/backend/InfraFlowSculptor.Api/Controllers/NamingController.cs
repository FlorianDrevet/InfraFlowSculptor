using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Contracts.Naming;
using InfraFlowSculptor.Domain.Common.Features;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;

namespace InfraFlowSculptor.Api.Controllers;

public static class NamingController
{
    public static RouteGroupBuilder MapNamingEndpoints(this RouteGroupBuilder v1)
    {
        var naming = v1.MapGroup("/naming")
            .WithTags("Naming")
            .RequireFeature(Features.Organizations)
            .RequireFeature(Features.ProjectSetup);

        naming.MapPost("/preview", (NamingPreviewRequest request) =>
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

                var snapshot = new ModelSnapshot(
                    new ProjectSnapshot(request.ProjectName, request.ProjectCode, string.Empty),
                    request.Environments.Select(environment => new EnvironmentSnapshot(environment.Code, environment.Region)).ToArray(),
                    new NamingConventionSnapshot(
                        request.NamingPreset,
                        request.DefaultNameTemplate,
                        request.ResourceGroupNameTemplate),
                    request.Tags ?? new Dictionary<string, string>(),
                    []);
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

        return v1;
    }
}
