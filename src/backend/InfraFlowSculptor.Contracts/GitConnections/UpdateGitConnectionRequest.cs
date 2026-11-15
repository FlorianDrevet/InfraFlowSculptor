namespace InfraFlowSculptor.Contracts.GitConnections;

public sealed record UpdateGitConnectionRequest(
    int ExpectedVersion,
    string Name,
    string Organization,
    Guid? TenantId,
    DateTimeOffset? PersonalAccessTokenExpiresAt,
    string? PersonalAccessToken);
