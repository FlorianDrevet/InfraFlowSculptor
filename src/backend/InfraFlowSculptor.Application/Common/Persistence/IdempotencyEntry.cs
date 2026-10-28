namespace InfraFlowSculptor.Application.Common.Persistence;

public sealed record IdempotencyEntry(
    string RequestHash,
    IdempotencyResponse? Response);
