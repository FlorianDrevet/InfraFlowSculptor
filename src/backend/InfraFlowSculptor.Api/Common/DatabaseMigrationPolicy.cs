using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InfraFlowSculptor.Api.Common;

public static partial class DatabaseMigrationPolicy
{
    public static bool ShouldApplyMigrations(IHostEnvironment environment, IConfiguration configuration) =>
        (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        && (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("ifs"))
            || !string.IsNullOrWhiteSpace(configuration["Azure:ifs:Endpoint"]));

    public static async Task ApplyMigrationsIfNeededAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldApplyMigrations(app.Environment, app.Configuration))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IfsDbContext>();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);

        foreach (var migration in pendingMigrations)
        {
            MigrationApplied(app.Logger, migration);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied migration {MigrationName}")]
    private static partial void MigrationApplied(ILogger logger, string migrationName);
}
