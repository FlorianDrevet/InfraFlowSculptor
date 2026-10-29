using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Contracts.Projects;

public sealed record ApplyFindingFixRequest(
    ObjectType SubjectType,
    Guid SubjectId,
    Guid? EnvironmentId,
    int ExpectedObjectVersion,
    string? DetailKey);
