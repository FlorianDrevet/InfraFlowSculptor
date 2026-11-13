namespace InfraFlowSculptor.Domain.RevisionAggregate;

public sealed record RevisionFile(string RelativePath, string Sha256, long Size);
