using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using InfraFlowSculptor.Api.Tests.Common;

namespace InfraFlowSculptor.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string Environment { get; set; } = "Testing";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.UseSetting(
            "ConnectionStrings:servicebus",
            "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
        builder.UseSetting("Auth:Provider", "Keycloak");
        builder.UseSetting("Auth:Authority", "https://localhost:8080/realms/ifs");
        builder.UseSetting("Auth:Audience", TestTokens.Audience);
        builder.UseSetting("Auth:TestSigningKey", TestTokens.SigningKey);
    }
}
