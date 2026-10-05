using Microsoft.Extensions.DependencyInjection;
using Azure.Core;
using Azure.Storage.Blobs;
using HealthChecks.Azure.Storage.Blobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Infrastructure.Azure;
using InfraFlowSculptor.Infrastructure.Authentication;
using InfraFlowSculptor.Infrastructure.Persistence;
using InfraFlowSculptor.Infrastructure.Persistence.Admin;
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
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddScoped<ICurrentOrganization>(_ => NoCurrentOrganization.Instance);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<IVerifiedEmailResolver, VerifiedEmailResolver>();
        services.TryAddScoped<IIdempotencyStore, EfIdempotencyStore>();
        services.TryAddScoped<IdempotencyKeyPurge>();

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

    private sealed class NoCurrentOrganization : ICurrentOrganization
    {
        public static NoCurrentOrganization Instance { get; } = new();

        public OrganizationId? Id => null;
    }
}
