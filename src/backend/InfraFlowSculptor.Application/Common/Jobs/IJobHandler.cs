using System.Text.Json;

namespace InfraFlowSculptor.Application.Common.Jobs;

public interface IJobHandler
{
    Type JobType { get; }

    string HandlerType { get; }

    Task HandleAsync(JsonElement payload, JobExecutionContext context, CancellationToken cancellationToken);
}
