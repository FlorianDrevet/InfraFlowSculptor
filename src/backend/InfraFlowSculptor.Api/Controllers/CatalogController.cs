using System.Text.Json;
using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Contracts.Catalog;

namespace InfraFlowSculptor.Api.Controllers;

public static class CatalogController
{
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder v1)
    {
        var catalog = v1.MapGroup("/catalog").WithTags("Catalog");

        catalog.MapGet("/versions", (ICatalogProvider catalogProvider) =>
            {
                var versions = catalogProvider.GetVersions()
                    .Select(version => new CatalogVersionResponse(
                        version.Version,
                        version.Status,
                        version.PublishedAt,
                        version.SupportEndsAt,
                        version.Fingerprint))
                    .ToArray();
                return Results.Ok(versions);
            })
            .WithName(EndpointNames.GetCatalogVersions)
            .WithSummary("Lister les versions disponibles du catalogue.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .WithMetadata(new OrganizationAgnostic())
            .Produces<IReadOnlyList<CatalogVersionResponse>>(StatusCodes.Status200OK);

        catalog.MapGet("/{version}/types", (
                string version,
                string? category,
                string? search,
                ICatalogProvider catalogProvider) =>
            {
                var catalogVersion = catalogProvider.GetVersion(version);
                if (catalogVersion is null)
                {
                    return Results.NotFound();
                }

                var resourceTypes = catalogVersion.FindResourceTypes(category, search)
                    .Select(resourceType => new CatalogResourceTypeSummaryResponse(
                        resourceType.Type,
                        resourceType.Category,
                        resourceType.LabelFr,
                        resourceType.LabelEn,
                        resourceType.Icon))
                    .ToArray();
                return Results.Ok(resourceTypes);
            })
            .WithName(EndpointNames.GetCatalogTypes)
            .WithSummary("Rechercher les types d’une version du catalogue.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .WithMetadata(new OrganizationAgnostic())
            .Produces<IReadOnlyList<CatalogResourceTypeSummaryResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        catalog.MapGet("/{version}/types/{type}", (
                string version,
                string type,
                ICatalogProvider catalogProvider) =>
            {
                var descriptor = catalogProvider.GetVersion(version)?.GetResourceType(type);
                if (descriptor is null)
                {
                    return Results.NotFound();
                }

                return Results.Json(descriptor.Definition);
            })
            .WithName(EndpointNames.GetCatalogType)
            .WithSummary("Lire le descripteur complet d’un type de ressource.")
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .WithMetadata(new OrganizationAgnostic())
            .Produces<JsonElement>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return v1;
    }
}
