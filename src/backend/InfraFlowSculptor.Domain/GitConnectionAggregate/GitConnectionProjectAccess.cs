using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Domain.GitConnectionAggregate;

public sealed class GitConnectionProjectAccess
{
    private GitConnectionProjectAccess()
    {
    }

    public ProjectId ProjectId { get; private set; }

    internal GitConnectionProjectAccess(ProjectId projectId)
    {
        ProjectId = projectId;
    }
}
