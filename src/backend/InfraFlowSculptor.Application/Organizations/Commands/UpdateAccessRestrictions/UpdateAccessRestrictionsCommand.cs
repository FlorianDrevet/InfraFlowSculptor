using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Commands.UpdateAccessRestrictions;

[RequiresOrganizationRole(OrganizationRole.Administrator)]
[Audited(AuditActions.OrganizationAccessRestrictionsUpdated, ObjectType.Organization)]
public sealed record UpdateAccessRestrictionsCommand(
    Guid OrganizationId,
    IReadOnlyCollection<string> AllowedEmailDomains,
    IReadOnlyCollection<Guid> AllowedTenantIds)
    : ICommand<ErrorOr<OrganizationSummary>>;
