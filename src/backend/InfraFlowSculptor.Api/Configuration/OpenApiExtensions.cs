using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace InfraFlowSculptor.Api.Configuration;

public static class OpenApiExtensions
{
    public static IServiceCollection AddOpenApiExtensions(this IServiceCollection services, string authority)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(ScalarOAuth2.SecuritySchemeName, context.Document)] =
                            ScalarOAuth2.Scopes.ToList()
                    });
                }

                return Task.CompletedTask;
            });

            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "InfraFlowSculptor API v1";
                document.Servers = [];
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[ScalarOAuth2.SecuritySchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        AuthorizationCode = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri(ScalarOAuth2.AuthorizationUrl(authority)),
                            TokenUrl = new Uri(ScalarOAuth2.TokenUrl(authority)),
                            Scopes = new Dictionary<string, string>
                            {
                                [ScalarOAuth2.OpenIdScope] = "Authenticate the user",
                                [ScalarOAuth2.ProfileScope] = "Read the user's profile",
                                [ScalarOAuth2.EmailScope] = "Read the user's email address"
                            }
                        }
                    }
                };

                return Task.CompletedTask;
            });
        });
        return services;
    }
}
