using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using InfraFlowSculptor.Api.Tests.Common;

namespace InfraFlowSculptor.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private const string LocalAuthAuthority = "https://localhost:8080/realms/ifs";

    public string Environment { get; set; } = "Testing";
    public bool UseTestSigningKey { get; set; } = true;

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
                ["Auth:Authority"] = LocalAuthAuthority
            }));
        if (UseTestSigningKey)
        {
            builder.UseSetting("Auth:TestSigningKey", TestTokens.SigningKey);
        }
    }
}
