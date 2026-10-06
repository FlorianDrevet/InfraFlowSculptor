using System.Diagnostics;
using InfraFlowSculptor.Application.Common.Observability;
using InfraFlowSculptor.Application.Common.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace InfraFlowSculptor.Api.Observability;

public sealed class RequestLoggingScopeMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingScopeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ICurrentOrganization currentOrganization)
    {
        var projectId = context.GetRouteValue("projectId")?.ToString();
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        IfsTelemetry.SetCurrentActivityTags(currentOrganization.Id, projectId);

        using (IfsTelemetry.BeginLogScope(logger, currentOrganization.Id, projectId, traceId))
        {
            await next(context);
        }
    }
}
