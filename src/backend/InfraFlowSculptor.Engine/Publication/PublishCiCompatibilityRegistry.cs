using InfraFlowSculptor.Domain.GitConnectionAggregate;

namespace InfraFlowSculptor.Engine.Publication;

public sealed class PublishCiCompatibilityRegistry
{
    private readonly IReadOnlyDictionary<string, IReadOnlySet<GitProviderKind>> providersByCiPlatform;

    public PublishCiCompatibilityRegistry(IReadOnlyDictionary<string, IReadOnlySet<GitProviderKind>> providersByCiPlatform)
    {
        ArgumentNullException.ThrowIfNull(providersByCiPlatform);
        this.providersByCiPlatform = providersByCiPlatform;
    }

    public static PublishCiCompatibilityRegistry Pilot { get; } = new(
        new Dictionary<string, IReadOnlySet<GitProviderKind>>(StringComparer.OrdinalIgnoreCase)
        {
            [PublishCiPlatformCodes.AzureDevOps] = new HashSet<GitProviderKind> { GitProviderKind.AzureRepos, GitProviderKind.GitHub },
            [PublishCiPlatformCodes.GitHubActions] = new HashSet<GitProviderKind> { GitProviderKind.GitHub },
            [PublishCiPlatformCodes.GitLabCi] = new HashSet<GitProviderKind> { GitProviderKind.GitLab }
        });

    public bool IsCompatible(string ciPlatform, GitProviderKind providerKind) =>
        providersByCiPlatform.TryGetValue(ciPlatform, out var compatibleProviders)
        && compatibleProviders.Contains(providerKind);
}
