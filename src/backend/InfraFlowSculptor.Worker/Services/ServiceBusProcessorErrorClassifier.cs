using Azure.Messaging.ServiceBus;

namespace InfraFlowSculptor.Worker.Services;

internal static class ServiceBusProcessorErrorClassifier
{
    private const string InactiveAmqpLinkMessage =
        "did not have any active links in the past 300000 milliseconds";

    public static bool IsLocalServiceBusEmulator(
        bool explicitlyConfiguredAsEmulator,
        bool isDevelopment,
        string fullyQualifiedNamespace)
    {
        if (explicitlyConfiguredAsEmulator)
        {
            return true;
        }

        return isDevelopment
            && Uri.TryCreate($"sb://{fullyQualifiedNamespace}", UriKind.Absolute, out var endpoint)
            && endpoint.IsLoopback;
    }

    public static bool IsExpectedLocalEmulatorIdleAcceptSessionFailure(
        bool isLocalEmulator,
        ServiceBusErrorSource errorSource,
        Exception exception)
    {
        if (!isLocalEmulator || errorSource != ServiceBusErrorSource.AcceptSession)
        {
            return false;
        }

        if (exception is not ServiceBusException serviceBusException)
        {
            return false;
        }

        return serviceBusException.IsTransient
            && serviceBusException.Reason == ServiceBusFailureReason.GeneralError
            && serviceBusException.Message.Contains(InactiveAmqpLinkMessage, StringComparison.OrdinalIgnoreCase);
    }
}
