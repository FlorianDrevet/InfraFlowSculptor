using InfraFlowSculptor.Domain.Common.Extensibility;
using InfraFlowSculptor.Domain.GitConnectionAggregate;

namespace InfraFlowSculptor.Application.GitConnections;

public sealed class GitProviderRegistry(IEnumerable<IGitProvider> providers)
{
    private readonly Registry<GitProviderKind, IGitProvider> registry = new(providers);

    public IReadOnlyCollection<GitProviderKind> Keys => registry.Keys;

    public bool Supports(GitProviderKind kind) => registry.Supports(kind);

    public IGitProvider Get(GitProviderKind kind) => registry.Get(kind);
}
