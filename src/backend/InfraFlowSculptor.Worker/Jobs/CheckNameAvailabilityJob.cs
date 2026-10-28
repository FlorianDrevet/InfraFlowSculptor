using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Naming.Availability;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed class CheckNameAvailabilityJob(NameAvailabilityProcessor processor) : IScheduledJob
{
    private static readonly TimeSpan ProcessingInterval = TimeSpan.FromSeconds(30);

    public string Name => "check-name-availability";

    public TimeSpan Interval => ProcessingInterval;

    public Task RunAsync(CancellationToken cancellationToken) =>
        processor.ProcessPendingAsync(cancellationToken);
}
