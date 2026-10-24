using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
builder.Services.AddSingleton<SqlConnectionStringFactory>();
builder.Services.AddSingleton<IDependencyCheck, SecretDependencyCheck>();
builder.Services.AddSingleton<IDependencyCheck, LogsDependencyCheck>();
builder.Services.AddSingleton<IDependencyCheck, SqlDependencyCheck>();
builder.Services.AddSingleton<IDependencyCheck, SqlLeastPrivilegeDependencyCheck>();
builder.Services.AddSingleton<IDependencyCheck, AppConfigurationDependencyCheck>();
builder.Services.AddSingleton<IDependencyCheck, ServiceBusDependencyCheck>();
builder.Services.AddSingleton<DependencyHealthService>();

var app = builder.Build();
app.MapGet("/health", (IConfiguration configuration) =>
    Results.Ok(new { status = "ok", version = configuration["IMAGE_TAG"] ?? "unknown" }));
app.MapGet("/health/dependencies", async (
    DependencyHealthService service,
    CancellationToken cancellationToken) =>
{
    var report = await service.RunAsync(cancellationToken);
    return Results.Json(
        new { checks = report.Checks },
        statusCode: report.IsHealthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});
app.Run();

public partial class Program
{
}
