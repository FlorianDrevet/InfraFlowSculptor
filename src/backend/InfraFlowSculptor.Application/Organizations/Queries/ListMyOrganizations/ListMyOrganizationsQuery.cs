using ErrorOr;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Queries.ListMyOrganizations;

public sealed record ListMyOrganizationsQuery : IQuery<ErrorOr<IReadOnlyList<OrganizationSummary>>>;
