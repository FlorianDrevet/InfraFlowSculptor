namespace InfraFlowSculptor.Application.Common.Persistence;

public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("A concurrent update changed the entity before this write completed.", innerException);
