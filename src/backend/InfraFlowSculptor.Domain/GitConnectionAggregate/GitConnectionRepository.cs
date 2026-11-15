using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Domain.GitConnectionAggregate;

public sealed class GitConnectionRepository
{
    private readonly List<GitConnectionProjectAccess> allowedProjects = [];

    private GitConnectionRepository()
    {
    }

    public string ProviderRepositoryId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string DefaultBranch { get; private set; } = string.Empty;

    public bool OpenToAllProjects { get; private set; }

    public IReadOnlyCollection<GitConnectionProjectAccess> AllowedProjects => allowedProjects.AsReadOnly();

    public static GitConnectionRepository Create(string providerRepositoryId, string name, string defaultBranch) =>
        new()
        {
            ProviderRepositoryId = providerRepositoryId,
            Name = name,
            DefaultBranch = defaultBranch
        };

    public bool CanAccess(ProjectId projectId) =>
        OpenToAllProjects || allowedProjects.Any(access => access.ProjectId == projectId);

    internal void OpenToAll()
    {
        OpenToAllProjects = true;
        allowedProjects.Clear();
    }

    internal void OpenTo(IEnumerable<ProjectId> projectIds)
    {
        OpenToAllProjects = false;
        allowedProjects.Clear();
        allowedProjects.AddRange(projectIds.Select(projectId => new GitConnectionProjectAccess(projectId)));
    }

    internal void Close()
    {
        OpenToAllProjects = false;
        allowedProjects.Clear();
    }
}
