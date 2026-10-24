using Azure.Core;
using Azure.Monitor.Query.Logs;
using Microsoft.Extensions.Configuration;

public sealed class LogsDependencyCheck(IConfiguration configuration, TokenCredential credential) : IDependencyCheck
{
    public string Name => "logs";

    public async Task<DependencyCheckOutcome?> CheckAsync(CancellationToken cancellationToken)
    {
        var workspaceId = configuration["LogAnalytics:WorkspaceId"];
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return null;
        }

        var client = new LogsQueryClient(credential);
        await client.QueryWorkspaceAsync(
            workspaceId,
            "print 1",
            new LogsQueryTimeRange(TimeSpan.FromMinutes(5)),
            cancellationToken: cancellationToken);
        return new DependencyCheckOutcome(true, null);
    }
}
