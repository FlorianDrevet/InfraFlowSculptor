using ErrorOr;
using InfraFlowSculptor.Application.Organizations;
using Mediator;

namespace InfraFlowSculptor.Application.Organizations.Queries.ListOrganizationMembers;

public sealed record ListOrganizationMembersQuery(Guid OrganizationId)
    : IQuery<ErrorOr<IReadOnlyList<OrganizationMemberSummary>>>;
