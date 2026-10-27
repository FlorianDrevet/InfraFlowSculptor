using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Projects.Commands.TakeProjectOwnership;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
[RequiresPermission(Permission.ProjectRead)]
[Audited(AuditActions.ProjectOwnershipTaken, ObjectType.ProjectRoleAssignment)]
public sealed record TakeProjectOwnershipCommand(Guid ProjectId, int ExpectedVersion)
    : ICommand<ErrorOr<ProjectRoleAssignmentSummary>>, IProjectScopedRequest;
