using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Contracts.Authentication;

namespace InfraFlowSculptor.Api.Controllers;

public static class MeController
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/me", (ICurrentUser currentUser) =>
                TypedResults.Ok(new MeResponse(
                    currentUser.Key.TenantId,
                    currentUser.Key.ObjectId,
                    currentUser.DisplayName,
                    currentUser.VerifiedEmail?.Value)))
            .WithName(EndpointNames.GetMe)
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .Produces<MeResponse>(StatusCodes.Status200OK);

        return v1;
    }
}
