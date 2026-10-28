using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Users.Commands.UpdateUserPreferences;
using InfraFlowSculptor.Contracts.Authentication;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Api.Controllers;

public static class MeController
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/me", async (
                ICurrentUser currentUser,
                [Microsoft.AspNetCore.Mvc.FromServices] IIfsDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var profile = await dbContext.Query<UserProfile>()
                    .SingleOrDefaultAsync(
                        candidate => candidate.TenantId == currentUser.Key.TenantId
                            && candidate.ObjectId == currentUser.Key.ObjectId,
                        cancellationToken);

                return profile is null
                    ? Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "The requested resource was not found.",
                        extensions: new Dictionary<string, object?> { ["code"] = "NOT_FOUND" })
                    : Results.Ok(new MeResponse(
                        profile.TenantId,
                        profile.ObjectId,
                        profile.DisplayName,
                        profile.Email,
                        profile.PreferredLanguage,
                        profile.PreferredTheme));
            })
            .WithName(EndpointNames.GetMe)
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Read)
            .WithMetadata(new OrganizationAgnostic())
            .Produces<MeResponse>(StatusCodes.Status200OK);

        v1.MapPut("/me/preferences", async (
                UpdateMePreferencesRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new UpdateUserPreferencesCommand(request.PreferredLanguage, request.PreferredTheme),
                    cancellationToken);
                return result.Match<IResult>(
                    preferences => Results.Ok(new MePreferencesResponse(
                        preferences.PreferredLanguage,
                        preferences.PreferredTheme)),
                    errors => errors.ToProblem());
            })
            .WithName(EndpointNames.UpdateMePreferences)
            .RequireAuthorization(AuthorizationPolicies.Member)
            .RequireRateLimiting(RateLimitingPolicies.Write)
            .WithMetadata(new OrganizationAgnostic())
            .Produces<MePreferencesResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return v1;
    }
}
