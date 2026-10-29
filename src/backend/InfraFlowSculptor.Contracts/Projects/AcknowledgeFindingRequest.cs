using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Contracts.Projects;

public sealed record AcknowledgeFindingRequest(
    ObjectType SubjectType,
    Guid SubjectId,
    Guid? EnvironmentId,
    int ExpectedObjectVersion,
    string Comment,
    string? DetailKey = null);
