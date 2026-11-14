namespace InfraFlowSculptor.Application.GitConnections;

public sealed record GitCommitChange(string Path, string? Content, GitCommitChangeKind Kind);
