namespace InfraFlowSculptor.Application.Common.Persistence;

public sealed record IdempotencyResponse(
    int StatusCode,
    string? ContentType,
    IReadOnlyDictionary<string, string[]> Headers,
    byte[] Body);
