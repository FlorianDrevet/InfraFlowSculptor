using InfraFlowSculptor.Application.Common.Features;
using InfraFlowSculptor.Application.Common.Security;

namespace InfraFlowSculptor.Api.Common;

public sealed class FeatureAvailabilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IFeatureAvailability featureAvailability,
        ICurrentOrganization currentOrganization)
    {
        var requirements = context.GetEndpoint()?.Metadata.GetOrderedMetadata<RequiredFeature>();
        if (requirements is not null)
        {
            foreach (var requirement in requirements)
            {
                if (!await featureAvailability.IsEnabledAsync(
                        requirement.Key,
                        currentOrganization.Id,
                        context.RequestAborted))
                {
                    await Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "The requested resource was not found.",
                        extensions: new Dictionary<string, object?> { ["code"] = "NOT_FOUND" })
                        .ExecuteAsync(context);
                    return;
                }
            }
        }

        await next(context);
    }
}
