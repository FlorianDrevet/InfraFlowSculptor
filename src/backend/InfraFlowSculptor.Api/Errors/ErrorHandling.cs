using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace InfraFlowSculptor.Api.Errors;

public static class ErrorHandling
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseExceptionHandler(exceptionHandlerApp =>
            exceptionHandlerApp.Run(async context =>
            {
                var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

                await Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "An unexpected error occurred.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "INTERNAL",
                        ["traceId"] = traceId
                    }).ExecuteAsync(context);
            }));
    }
}
