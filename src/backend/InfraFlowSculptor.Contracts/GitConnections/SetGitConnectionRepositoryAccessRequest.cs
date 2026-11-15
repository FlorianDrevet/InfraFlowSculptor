namespace InfraFlowSculptor.Contracts.GitConnections;

public sealed record SetGitConnectionRepositoryAccessRequest(
    int ExpectedVersion,
    string ProviderRepositoryId,
    bool OpenToAllProjects,
    IReadOnlyCollection<Guid> ProjectIds);
