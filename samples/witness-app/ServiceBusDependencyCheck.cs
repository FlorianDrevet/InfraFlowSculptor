using Azure.Core;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

public sealed class ServiceBusDependencyCheck(
    IConfiguration configuration,
    TokenCredential credential) : IDependencyCheck
{
    public string Name => "servicebus";

    public async Task<DependencyCheckOutcome?> CheckAsync(CancellationToken cancellationToken)
    {
        var namespaceName = configuration["ServiceBus:Namespace"];
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return null;
        }

        await using var client = new ServiceBusClient(namespaceName, credential);
        await using var sender = client.CreateSender("order-created");
        var message = new ServiceBusMessage("witness-check")
        {
            SessionId = Guid.NewGuid().ToString("N")
        };
        await sender.SendMessageAsync(message, cancellationToken);
        return new DependencyCheckOutcome(true, null);
    }
}
