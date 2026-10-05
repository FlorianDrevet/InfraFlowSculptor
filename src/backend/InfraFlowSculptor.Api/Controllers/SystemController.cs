using System.Reflection;
using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Contracts.System;

namespace InfraFlowSculptor.Api.Controllers;

public static class SystemController
{
    public static RouteGroupBuilder MapSystemEndpoints(this RouteGroupBuilder v1)
    {
        v1.MapGet("/version", (IHostEnvironment environment) =>
            {
                var version = typeof(SystemController).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "unknown";

                return new VersionResponse(version, environment.EnvironmentName);
            })
            .WithName(EndpointNames.GetVersion)
            .WithSummary("Get API version and environment.")
            .Produces<VersionResponse>(StatusCodes.Status200OK)
            .AllowAnonymous();

        return v1;
    }
}
