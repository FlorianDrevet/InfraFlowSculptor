using ErrorOr;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Queries.ListAuditEvents;

public sealed class ListAuditEventsQueryHandler(
    IAuditEventReader auditEventReader,
    ICurrentOrganization currentOrganization)
    : IQueryHandler<ListAuditEventsQuery, ErrorOr<AuditEventPage>>
{
    public async ValueTask<ErrorOr<AuditEventPage>> Handle(
        ListAuditEventsQuery query,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId
            || organizationId.Value != query.OrganizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        if (query.From > query.To)
        {
            return Error.Validation("AUDIT_PERIOD_INVALID", "The start of the audit period must be before its end.");
        }

        if (query.Page < 1 || query.PageSize is < 1 or > AuditPageLimits.MaximumPageSize)
        {
            return Error.Validation(
                "AUDIT_PAGE_INVALID",
                $"Page must be positive and page size must not exceed {AuditPageLimits.MaximumPageSize}.");
        }

        return await auditEventReader.ListAsync(
            new OrganizationId(query.OrganizationId),
            query.From,
            query.To,
            query.Page,
            query.PageSize,
            cancellationToken);
    }
}
