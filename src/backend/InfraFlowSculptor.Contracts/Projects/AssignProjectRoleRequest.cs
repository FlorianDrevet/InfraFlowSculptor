using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Contracts.Projects;

public sealed record AssignProjectRoleRequest(
    ProjectRole Role,
    ProjectRoleScope Scope,
    int ExpectedVersion);
