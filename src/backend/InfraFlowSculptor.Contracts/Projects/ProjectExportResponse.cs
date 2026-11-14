namespace InfraFlowSculptor.Contracts.Projects;

public sealed record ProjectExportResponse(string DownloadUrl, DateTimeOffset ExpiresAt);
