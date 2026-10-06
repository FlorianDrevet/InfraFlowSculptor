namespace InfraFlowSculptor.Application.Common.Jobs;

public sealed record PingJob(int DelayMilliseconds = 0) : IJob;
