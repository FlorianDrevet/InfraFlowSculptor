using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.RenameOrganization;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
[Audited(AuditActions.OrganizationRenamed, ObjectType.Organization)]
public sealed record RenameOrganizationCommand(Guid Id, string Name) : ICommand<ErrorOr<OrganizationSummary>>;
