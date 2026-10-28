using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Queries.ListAuditEvents;

[RequiresOrganizationRole(
    OrganizationRole.Administrator,
    OrganizationRole.Auditor,
    Permission = AuditPermissionNames.Read)]
public sealed record ListAuditEventsQuery(
    Guid OrganizationId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize) : IQuery<ErrorOr<AuditEventPage>>;
