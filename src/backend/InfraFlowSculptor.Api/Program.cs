using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using InfraFlowSculptor.Api;
using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Authentication;
using InfraFlowSculptor.Api.Common.RateLimiting;
using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Api.Controllers;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Infrastructure;
using InfraFlowSculptor.Infrastructure.Authentication;
using InfraFlowSculptor.Infrastructure.Configuration;
using InfraFlowSculptor.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var isOpenApiBuild = OpenApiBuildContext.IsDocumentGeneration;

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

// Container Apps' ingress is the only external route to the application container.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection(ConfigurationKeys.CorsAllowedOrigins)
            .Get<string[]>() ?? [];

        policy.AllowAnyHeader().AllowAnyMethod();

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
    });
});

var authAuthority = isOpenApiBuild
    ? AuthOptions.LocalKeycloakAuthority
    : builder.Configuration[AuthOptions.AuthorityConfigurationKey]
        ?? throw new InvalidOperationException($"{AuthOptions.AuthorityConfigurationKey} is required.");
builder.Services.AddOpenApiExtensions(authAuthority);
builder.Services.AddRateLimiting();
builder.Services.AddApplication();
if (isOpenApiBuild)
{
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<IVerifiedEmailResolver, VerifiedEmailResolver>();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
}
else
{
    builder.Services
        .AddInfrastructure(builder.Configuration)
        .AddPresentation(builder.Configuration, builder.Environment);
    builder.AddIfsDbContext("ifs");
}

var app = builder.Build();

if (!isOpenApiBuild)
{
    await app.ApplyMigrationsIfNeededAsync();

    app.UseForwardedHeaders();
    app.UseErrorHandling();
    app.UseStatusCodePages();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapDefaultEndpoints();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.AddDocuments("v1");
        options.WithTitle("InfraFlowSculptor API v1");
        options.AddAuthorizationCodeFlow(ScalarOAuth2.SecuritySchemeName, flow =>
        {
            flow.ClientId = ScalarOAuth2.ClientId;
            flow.AuthorizationUrl = ScalarOAuth2.AuthorizationUrl(authAuthority);
            flow.TokenUrl = ScalarOAuth2.TokenUrl(authAuthority);
            flow.Pkce = Pkce.Sha256;
            flow.SelectedScopes = ScalarOAuth2.Scopes;
        });
    }).AllowAnonymous();
}

var v1 = app.MapGroup("/v1");
v1.MapSystemEndpoints();
v1.MapMeEndpoints();
if (!isOpenApiBuild && (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")))
{
    v1.MapDevelopmentEndpoints();
}
if (!isOpenApiBuild)
{
    app.MapFallback(() => Results.NotFound()).AllowAnonymous();
}

app.Run();

public partial class Program
{
}
