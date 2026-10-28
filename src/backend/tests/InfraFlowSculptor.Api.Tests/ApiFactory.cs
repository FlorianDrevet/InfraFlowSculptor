using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using InfraFlowSculptor.Api.Tests.Common;
using Testcontainers.PostgreSql;

namespace InfraFlowSculptor.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string LocalAuthAuthority = "https://localhost:8080/realms/ifs";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ifs_api_tests")
        .WithUsername("ifs")
        .WithPassword("ifs_api_test_password")
        .Build();

    public string Environment { get; set; } = "Testing";
    public bool UseTestSigningKey { get; set; } = true;

    public Task InitializeAsync() => database.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await database.DisposeAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.UseSetting(
            "ConnectionStrings:servicebus",
            "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
        builder.UseSetting("Auth:Provider", "Keycloak");
        builder.UseSetting("Auth:Authority", LocalAuthAuthority);
        builder.UseSetting("Auth:Audience", TestTokens.Audience);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = LocalAuthAuthority,
                ["ConnectionStrings:ifs"] = database.GetConnectionString()
            }));
        if (UseTestSigningKey)
        {
            builder.UseSetting("Auth:TestSigningKey", TestTokens.SigningKey);
        }
    }
}
