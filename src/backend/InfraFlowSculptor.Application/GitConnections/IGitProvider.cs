using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.Common.Extensibility;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Application.GitConnections;

public interface IGitProvider : IKeyed<GitProviderKind>
{
    string DisplayName { get; }

    Task<IReadOnlyCollection<GitRepositoryInfo>> GetRepositoriesAsync(
        GitConnection connection,
        string? transientAccessToken,
        CancellationToken cancellationToken);
}
