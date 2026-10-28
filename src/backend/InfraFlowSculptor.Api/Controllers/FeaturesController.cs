using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Application.Common.Features;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Contracts.Features;
using InfraFlowSculptor.Domain.Common.Features;

namespace InfraFlowSculptor.Api.Controllers;

public static class FeaturesController
{
    public static RouteGroupBuilder MapFeatureEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/features", async (
                [Microsoft.AspNetCore.Mvc.FromServices] IFeatureAvailability featureAvailability,
                [Microsoft.AspNetCore.Mvc.FromServices] ICurrentOrganization currentOrganization,
                CancellationToken cancellationToken) =>
            {
                var features = new List<FeatureAvailabilityResponse>(FeatureCatalog.All.Count);
                foreach (var feature in FeatureCatalog.All)
                {
                    var enabled = await featureAvailability.IsEnabledAsync(
                        feature.Key,
                        currentOrganization.Id,
                        cancellationToken);
                    features.Add(new FeatureAvailabilityResponse(
                        feature.Key,
                        feature.Status.ToString(),
                        enabled));
                }

                return Results.Ok(features);
            })
            .WithName(EndpointNames.GetFeatures)
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .WithMetadata(new OrganizationAgnostic())
            .Produces<IReadOnlyList<FeatureAvailabilityResponse>>(StatusCodes.Status200OK);

        return v1;
    }
}
