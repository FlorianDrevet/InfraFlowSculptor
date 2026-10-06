using System.Text.Json;

namespace InfraFlowSculptor.Application.Common.Jobs;

public abstract class JobHandler<TJob> : IJobHandler<TJob>, IJobHandler
    where TJob : IJob
{
    public Type JobType => typeof(TJob);

    public string HandlerType => GetType().FullName ?? GetType().Name;

    public async Task HandleAsync(
        JsonElement payload,
        JobExecutionContext context,
        CancellationToken cancellationToken)
    {
        var job = payload.Deserialize<TJob>(JobSerialization.Options)
            ?? throw new InvalidOperationException($"The payload for job '{typeof(TJob).FullName}' was empty.");

        await HandleAsync(job, context, cancellationToken);
    }

    public abstract Task HandleAsync(
        TJob job,
        JobExecutionContext context,
        CancellationToken cancellationToken);
}
