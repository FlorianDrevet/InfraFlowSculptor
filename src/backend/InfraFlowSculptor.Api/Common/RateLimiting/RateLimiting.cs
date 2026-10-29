using System.Globalization;
using System.Diagnostics;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using InfraFlowSculptor.Api.Common;

namespace InfraFlowSculptor.Api.Common.RateLimiting;

public static class DependencyInjection
{
    private const int TestingPermitLimit = 10_000;

    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IHostEnvironment environment)
    {
        var readPermitLimit = environment.IsEnvironment("Testing") ? TestingPermitLimit : 600;
        var writePermitLimit = environment.IsEnvironment("Testing") ? TestingPermitLimit : 120;
        var generatePermitLimit = environment.IsEnvironment("Testing") ? TestingPermitLimit : 10;
        var publishPermitLimit = environment.IsEnvironment("Testing") ? TestingPermitLimit : 5;
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitingPolicies.Read, context => CreatePartition(context, readPermitLimit));
            options.AddPolicy(RateLimitingPolicies.Write, context => CreatePartition(context, writePermitLimit));
            options.AddPolicy(RateLimitingPolicies.Generate, context => CreatePartition(context, generatePermitLimit));
            options.AddPolicy(RateLimitingPolicies.Publish, context => CreatePartition(context, publishPermitLimit));

            options.OnRejected = async (rejection, cancellationToken) =>
            {
                var httpContext = rejection.HttpContext;
                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                        .ToString(CultureInfo.InvariantCulture);
                }

                await Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Too many requests.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "RATE_LIMITED",
                        ["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier
                    }).ExecuteAsync(httpContext);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> CreatePartition(HttpContext context, int permitLimit)
    {
        var objectId = context.User.FindFirst("oid")?.Value;
        var partitionKey = !string.IsNullOrWhiteSpace(objectId)
            ? $"oid:{objectId}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            });
    }
}
