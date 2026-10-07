using Azure.Messaging.ServiceBus;
using InfraFlowSculptor.Worker.Services;

namespace InfraFlowSculptor.Infrastructure.Tests.Jobs;

public sealed class ServiceBusProcessorErrorClassifierTests
{
    private const string InactiveAmqpLinkMessage =
        "The connection was closed by container because it did not have any active links in the past 300000 milliseconds.";

    [Fact]
    public void IdentifiesExplicitlyConfiguredLocalEmulator()
    {
        Assert.True(ServiceBusProcessorErrorClassifier.IsLocalServiceBusEmulator(
            explicitlyConfiguredAsEmulator: true,
            isDevelopment: false,
            fullyQualifiedNamespace: "namespace.servicebus.windows.net"));
    }

    [Fact]
    public void IdentifiesLoopbackEndpointOnlyInDevelopment()
    {
        Assert.True(ServiceBusProcessorErrorClassifier.IsLocalServiceBusEmulator(
            explicitlyConfiguredAsEmulator: false,
            isDevelopment: true,
            fullyQualifiedNamespace: "localhost:28181"));

        Assert.False(ServiceBusProcessorErrorClassifier.IsLocalServiceBusEmulator(
            explicitlyConfiguredAsEmulator: false,
            isDevelopment: false,
            fullyQualifiedNamespace: "localhost:28181"));

        Assert.False(ServiceBusProcessorErrorClassifier.IsLocalServiceBusEmulator(
            explicitlyConfiguredAsEmulator: false,
            isDevelopment: true,
            fullyQualifiedNamespace: "namespace.servicebus.windows.net"));
    }

    [Fact]
    public void RecognizesOnlyTransientIdleAcceptSessionFailures()
    {
        var exception = new ServiceBusException(
            isTransient: true,
            message: InactiveAmqpLinkMessage,
            reason: ServiceBusFailureReason.GeneralError);

        Assert.True(exception.IsTransient);
        Assert.Equal(ServiceBusFailureReason.GeneralError, exception.Reason);
        Assert.Contains(
            "did not have any active links in the past 300000 milliseconds",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.True(ServiceBusProcessorErrorClassifier.IsExpectedLocalEmulatorIdleAcceptSessionFailure(
            true,
            ServiceBusErrorSource.AcceptSession,
            exception));
    }

    [Fact]
    public void DoesNotClassifyTheSameFailureWhenTheConnectionIsNotTheLocalEmulator()
    {
        var exception = CreateIdleLinkException();

        Assert.False(ServiceBusProcessorErrorClassifier.IsExpectedLocalEmulatorIdleAcceptSessionFailure(
            false,
            ServiceBusErrorSource.AcceptSession,
            exception));
    }

    [Fact]
    public void DoesNotClassifyErrorsFromOtherProcessorOperations()
    {
        var exception = CreateIdleLinkException();

        Assert.False(ServiceBusProcessorErrorClassifier.IsExpectedLocalEmulatorIdleAcceptSessionFailure(
            true,
            ServiceBusErrorSource.RenewLock,
            exception));
    }

    [Fact]
    public void DoesNotClassifyNonTransientFailures()
    {
        var exception = new ServiceBusException(
            isTransient: false,
            message: InactiveAmqpLinkMessage,
            reason: ServiceBusFailureReason.GeneralError);

        Assert.False(ServiceBusProcessorErrorClassifier.IsExpectedLocalEmulatorIdleAcceptSessionFailure(
            true,
            ServiceBusErrorSource.AcceptSession,
            exception));
    }

    [Fact]
    public void DoesNotClassifyOtherTransientGeneralErrors()
    {
        var exception = new ServiceBusException(
            isTransient: true,
            message: "Service unavailable.",
            reason: ServiceBusFailureReason.GeneralError);

        Assert.False(ServiceBusProcessorErrorClassifier.IsExpectedLocalEmulatorIdleAcceptSessionFailure(
            true,
            ServiceBusErrorSource.AcceptSession,
            exception));
    }

    [Fact]
    public void DoesNotClassifyAnotherServiceBusFailureReason()
    {
        var exception = new ServiceBusException(
            isTransient: true,
            message: InactiveAmqpLinkMessage,
            reason: ServiceBusFailureReason.ServiceCommunicationProblem);

        Assert.False(ServiceBusProcessorErrorClassifier.IsExpectedLocalEmulatorIdleAcceptSessionFailure(
            true,
            ServiceBusErrorSource.AcceptSession,
            exception));
    }

    private static ServiceBusException CreateIdleLinkException() => new(
        isTransient: true,
        message: InactiveAmqpLinkMessage,
        reason: ServiceBusFailureReason.GeneralError);
}
