using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.GitConnections;

public sealed record GitConnectionRepositorySummary(
    string ProviderRepositoryId,
    string Name,
    string DefaultBranch,
    bool OpenToAllProjects,
    IReadOnlyCollection<ProjectId> AllowedProjects);
