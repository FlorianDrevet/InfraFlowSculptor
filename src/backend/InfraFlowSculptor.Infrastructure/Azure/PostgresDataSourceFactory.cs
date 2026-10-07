using Azure.Core;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace InfraFlowSculptor.Infrastructure.Azure;

public static class PostgresDataSourceFactory
{
    public const string TokenScope = "https://ossrdbms-aad.database.windows.net/.default";

    private static readonly TimeSpan TokenRefreshInterval = TimeSpan.FromMinutes(50);
    private static readonly TimeSpan TokenRetryInterval = TimeSpan.FromMinutes(5);

    public static NpgsqlDataSource Create(
        IConfiguration configuration,
        string name,
        TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(credential);

        var connectionString = configuration.GetConnectionString(name);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new NpgsqlDataSourceBuilder(connectionString).Build();
        }

        var endpoint = RequiredSetting(configuration, $"Azure:{name}:Endpoint");
        var database = RequiredSetting(configuration, $"Azure:{name}:Database");
        var username = RequiredSetting(configuration, $"Azure:{name}:Username");
        var normalizedEndpoint = endpoint.Contains("://", StringComparison.Ordinal)
            ? endpoint
            : $"https://{endpoint}";

        if (!Uri.TryCreate(normalizedEndpoint, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host)
            || uri.Scheme is not ("https" or "postgres"))
        {
            throw new InvalidOperationException($"Azure resource '{name}' has an invalid PostgreSQL endpoint.");
        }

        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Database = database,
            Username = username,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            SslMode = SslMode.Require
        };
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connection.ConnectionString);
        dataSourceBuilder.UsePeriodicPasswordProvider(
            async (_, cancellationToken) =>
            {
                var token = await credential.GetTokenAsync(
                    new TokenRequestContext([TokenScope]),
                    cancellationToken);
                return token.Token;
            },
            TokenRefreshInterval,
            TokenRetryInterval);

        return dataSourceBuilder.Build();
    }

    private static string RequiredSetting(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"PostgreSQL requires configuration value '{key}'.");
        }

        return value;
    }
}
