using Microsoft.Extensions.DependencyInjection;
using Azure.Core;
using Azure.Storage.Blobs;
using HealthChecks.Azure.Storage.Blobs;
using InfraFlowSculptor.Infrastructure.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Azure;

namespace InfraFlowSculptor.Infrastructure;

public static class DependencyInjection
{
    private static readonly string[] ServiceBusQueues = ["generation", "publication", "tracking", "notifications"];

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAzureClientsCore();

        var credential = new IfsAzureCredential();
        services.TryAddSingleton(credential);
        services.TryAddSingleton<TokenCredential>(credential);

        services
            .AddIfsBlobServiceClient("blobs")
            .AddIfsServiceBusClient("servicebus")
            .AddIfsRedis("redis");

        var healthChecks = services.AddHealthChecks()
            .AddRedis(
                serviceProvider => serviceProvider.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>(),
                name: "redis",
                timeout: TimeSpan.FromSeconds(10));

        healthChecks.Add(new HealthCheckRegistration(
            "blobs",
            serviceProvider => new AzureBlobStorageHealthCheck(
                serviceProvider.GetRequiredService<BlobServiceClient>()),
            failureStatus: null,
            tags: null,
            timeout: TimeSpan.FromSeconds(10)));

        var serviceBus = AzureConnectionResolver.Resolve(configuration, "servicebus", credential);
        foreach (var queue in ServiceBusQueues)
        {
            if (serviceBus.Mode == AzureConnectionMode.ConnectionString)
            {
                healthChecks.AddAzureServiceBusQueue(
                    serviceBus.ConnectionString!,
                    queue,
                    name: $"servicebus-{queue}",
                    timeout: TimeSpan.FromSeconds(10));
            }
            else
            {
                healthChecks.AddAzureServiceBusQueue(
                    serviceBus.Endpoint!.Host,
                    queue,
                    serviceBus.Credential!,
                    name: $"servicebus-{queue}",
                    timeout: TimeSpan.FromSeconds(10));
            }
        }

        return services;
    }
}
