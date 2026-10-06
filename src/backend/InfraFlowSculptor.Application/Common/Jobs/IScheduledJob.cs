namespace InfraFlowSculptor.Application.Common.Jobs;

public interface IScheduledJob
{
    string Name { get; }

    TimeSpan Interval { get; }

    Task RunAsync(CancellationToken cancellationToken);
}
