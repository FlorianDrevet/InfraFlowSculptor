namespace InfraFlowSculptor.Api.Configuration;

public static class OpenApiExtensions
{
    public static IServiceCollection AddOpenApiExtensions(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "InfraFlowSculptor API v1";
                return Task.CompletedTask;
            }));
        return services;
    }
}
