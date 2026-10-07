namespace InfraFlowSculptor.Application.Common.Jobs;

public interface IJobHandler<TJob> where TJob : IJob
{
    Task HandleAsync(TJob job, JobExecutionContext context, CancellationToken cancellationToken);
}
