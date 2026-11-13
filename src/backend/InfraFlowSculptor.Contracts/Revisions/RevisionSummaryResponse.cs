namespace InfraFlowSculptor.Contracts.Revisions;

public sealed record RevisionSummaryResponse(
    Guid Id,
    int Number,
    string State,
    Guid AuthorTenantId,
    Guid AuthorObjectId,
    int ModelVersion,
    string CatalogVersion,
    string InfrastructureLanguage,
    string CiPlatform,
    string InputSha256,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureCode,
    bool IsStale,
    int? QueuePosition,
    int FindingsCount,
    int FilesCount,
    int ChangesCount);
