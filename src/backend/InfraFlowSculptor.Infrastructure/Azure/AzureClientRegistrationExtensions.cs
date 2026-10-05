using Azure.Messaging.ServiceBus;
using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Azure.StackExchangeRedis;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace InfraFlowSculptor.Infrastructure.Azure;

public static class AzureClientRegistrationExtensions
{
    public static IServiceCollection AddIfsBlobServiceClient(this IServiceCollection services, string name)
    {
        services.AddAzureConnection(name);
        services.AddSingleton(serviceProvider =>
        {
            var connection = serviceProvider.GetRequiredKeyedService<AzureConnection>(name);
            return connection.Mode switch
            {
                AzureConnectionMode.ConnectionString => new BlobServiceClient(connection.ConnectionString!),
                AzureConnectionMode.ManagedIdentity => new BlobServiceClient(
                    connection.Endpoint!,
                    connection.Credential!),
                _ => throw new InvalidOperationException($"Unsupported connection mode for '{name}'.")
            };
        });

        return services;
    }

    public static IServiceCollection AddIfsServiceBusClient(this IServiceCollection services, string name)
    {
        services.AddAzureConnection(name);
        services.AddSingleton(serviceProvider =>
        {
            var connection = serviceProvider.GetRequiredKeyedService<AzureConnection>(name);
            return connection.Mode switch
            {
                AzureConnectionMode.ConnectionString => new ServiceBusClient(connection.ConnectionString!),
                AzureConnectionMode.ManagedIdentity => new ServiceBusClient(
                    connection.Endpoint!.Host,
                    connection.Credential!),
                _ => throw new InvalidOperationException($"Unsupported connection mode for '{name}'.")
            };
        });

        return services;
    }

    public static IServiceCollection AddIfsRedis(this IServiceCollection services, string name)
    {
        services.AddAzureConnection(name);
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var connection = serviceProvider.GetRequiredKeyedService<AzureConnection>(name);
            if (connection.Mode == AzureConnectionMode.ConnectionString)
            {
                return ConnectionMultiplexer.ConnectAsync(connection.ConnectionString!)
                    .GetAwaiter()
                    .GetResult();
            }

            var endpoint = connection.Endpoint!;
            var port = endpoint.IsDefaultPort ? 10000 : endpoint.Port;
            var options = ConfigurationOptions.Parse($"{endpoint.Host}:{port}");
            options.Ssl = endpoint.Scheme is "https" or "rediss";
            options.AbortOnConnectFail = false;
            options.ConfigureForAzureWithTokenCredentialAsync(connection.Credential!)
                .GetAwaiter()
                .GetResult();

            return ConnectionMultiplexer.ConnectAsync(options).GetAwaiter().GetResult();
        });

        return services;
    }

    private static IServiceCollection AddAzureConnection(this IServiceCollection services, string name)
    {
        services.TryAddSingleton<IfsAzureCredential>();
        services.TryAddSingleton<TokenCredential>(serviceProvider =>
            serviceProvider.GetRequiredService<IfsAzureCredential>());
        services.AddKeyedSingleton<AzureConnection>(name, (serviceProvider, _) =>
            AzureConnectionResolver.Resolve(
                serviceProvider.GetRequiredService<IConfiguration>(),
                name,
                serviceProvider.GetRequiredService<IfsAzureCredential>()));

        return services;
    }
}
