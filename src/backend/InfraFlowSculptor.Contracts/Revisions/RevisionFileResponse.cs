namespace InfraFlowSculptor.Contracts.Revisions;

public sealed record RevisionFileResponse(string RelativePath, string Sha256, long Size);
