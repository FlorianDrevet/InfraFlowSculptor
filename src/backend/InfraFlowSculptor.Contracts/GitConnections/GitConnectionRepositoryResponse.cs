namespace InfraFlowSculptor.Contracts.GitConnections;

public sealed record GitConnectionRepositoryResponse(
    string ProviderRepositoryId,
    string Name,
    string DefaultBranch,
    bool OpenToAllProjects,
    IReadOnlyCollection<Guid> AllowedProjectIds);
