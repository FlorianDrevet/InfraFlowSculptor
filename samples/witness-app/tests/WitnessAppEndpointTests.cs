using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public sealed class WitnessAppEndpointTests
{
    [Fact]
    public async Task Dependencies_WhenConfigurationIsAbsent_ReturnsAllChecksAsSkipped()
    {
        await using var factory = new WitnessApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/dependencies");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var checks = document.RootElement.GetProperty("checks");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        checks.GetArrayLength().Should().Be(6);
        foreach (var check in checks.EnumerateArray())
        {
            check.GetProperty("ok").ValueKind.Should().Be(JsonValueKind.Null);
            check.GetProperty("error").GetString().Should().Be("skipped");
        }
    }

    [Fact]
    public async Task Dependencies_WhenCheckThrows_Returns503WithoutExceptionDetails()
    {
        const string secretValue = "do-not-return-this-secret";
        await using var factory = new WitnessApplicationFactory(services =>
        {
            services.RemoveAll<IDependencyCheck>();
            services.AddSingleton<IDependencyCheck>(new ThrowingDependencyCheck(secretValue));
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/dependencies");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
        body.Should().NotContain(secretValue);
        body.Should().Contain("dependency check failed");
    }

    [Fact]
    public async Task Health_WhenImageTagIsConfigured_ReturnsThatVersion()
    {
        await using var factory = new WitnessApplicationFactory(
            settings: new Dictionary<string, string?> { ["IMAGE_TAG"] = "local" });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        document.RootElement.GetProperty("status").GetString().Should().Be("ok");
        document.RootElement.GetProperty("version").GetString().Should().Be("local");
    }

    private sealed class WitnessApplicationFactory(
        Action<IServiceCollection>? configureServices = null,
        IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(settings ?? new Dictionary<string, string?>());
            });
            builder.ConfigureServices(services => configureServices?.Invoke(services));
        }
    }

    private sealed class ThrowingDependencyCheck(string secretValue) : IDependencyCheck
    {
        public string Name => "throwing";

        public Task<DependencyCheckOutcome?> CheckAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(secretValue);
        }
    }
}
