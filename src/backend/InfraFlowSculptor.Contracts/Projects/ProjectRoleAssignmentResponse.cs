using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Contracts.Projects;

public sealed record ProjectRoleAssignmentResponse(
    Guid MemberId,
    ProjectRole Role,
    ProjectRoleScope Scope,
    int Version,
    string Source);
