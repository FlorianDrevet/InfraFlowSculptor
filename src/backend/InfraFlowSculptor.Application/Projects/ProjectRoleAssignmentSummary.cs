using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Projects;

public sealed record ProjectRoleAssignmentSummary(
    Guid MemberId,
    ProjectRole Role,
    ProjectRoleScope Scope,
    int Version,
    string Source);
