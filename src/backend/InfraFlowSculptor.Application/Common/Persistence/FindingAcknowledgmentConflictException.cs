namespace InfraFlowSculptor.Application.Common.Persistence;

public sealed class FindingAcknowledgmentConflictException(Exception innerException)
    : Exception("The finding has already been acknowledged for the current object version.", innerException);
