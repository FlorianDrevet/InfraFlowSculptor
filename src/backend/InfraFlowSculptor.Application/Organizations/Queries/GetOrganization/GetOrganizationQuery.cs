using ErrorOr;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Queries.GetOrganization;

public sealed record GetOrganizationQuery(Guid Id) : IQuery<ErrorOr<OrganizationSummary>>;
