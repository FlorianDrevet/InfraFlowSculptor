using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace InfraFlowSculptor.Api.Common.Idempotency;

public static class IdempotencyEndpointExtensions
{
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder)
    {
        builder.AddEndpointFilterFactory((factoryContext, next) =>
        {
            var serviceChecker = factoryContext.ApplicationServices.GetService<IServiceProviderIsService>();
            var requestArgumentIndexes = factoryContext.MethodInfo.GetParameters()
                .Select((parameter, index) => (parameter, index))
                .Where(item => IsRequestArgument(item.parameter, serviceChecker))
                .Select(item => item.index)
                .ToArray();

            return invocationContext =>
            {
                var filter = invocationContext.HttpContext.RequestServices
                    .GetRequiredService<IdempotencyEndpointFilter>();
                return filter.InvokeAsync(invocationContext, next, requestArgumentIndexes);
            };
        });

        return builder;
    }

    private static bool IsRequestArgument(ParameterInfo parameter, IServiceProviderIsService? serviceChecker)
    {
        var type = parameter.ParameterType;
        if (type == typeof(CancellationToken)
            || typeof(HttpContext).IsAssignableFrom(type)
            || typeof(HttpRequest).IsAssignableFrom(type)
            || typeof(HttpResponse).IsAssignableFrom(type)
            || parameter.GetCustomAttribute<FromServicesAttribute>() is not null)
        {
            return false;
        }

        return serviceChecker?.IsService(type) != true;
    }
}
