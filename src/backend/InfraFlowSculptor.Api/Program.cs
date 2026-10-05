using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using InfraFlowSculptor.Api;
using InfraFlowSculptor.Api.Common;
using InfraFlowSculptor.Api.Common.RateLimiting;
using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Api.Controllers;
using InfraFlowSculptor.Api.Errors;
using InfraFlowSculptor.Application;
using InfraFlowSculptor.Infrastructure;
using InfraFlowSculptor.Infrastructure.Configuration;
using InfraFlowSculptor.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddOpenApiExtensions();
builder.Services.AddRateLimiting();
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation();
builder.AddIfsDbContext("ifs");

var app = builder.Build();

await app.ApplyMigrationsIfNeededAsync();

app.UseForwardedHeaders();
app.UseErrorHandling();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.AddDocuments("v1");
        options.WithTitle("InfraFlowSculptor API v1");
    });
}

var v1 = app.MapGroup("/v1");
v1.MapSystemEndpoints();

app.Run();

public partial class Program
{
}
