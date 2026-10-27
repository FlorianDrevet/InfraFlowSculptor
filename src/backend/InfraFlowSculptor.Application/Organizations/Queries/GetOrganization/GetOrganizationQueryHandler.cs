using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Queries.GetOrganization;

public sealed class GetOrganizationQueryHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization)
    : IQueryHandler<GetOrganizationQuery, ErrorOr<OrganizationSummary>>
{
    public async ValueTask<ErrorOr<OrganizationSummary>> Handle(
        GetOrganizationQuery query,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId || organizationId.Value != query.Id)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var profileId = await dbContext.Query<UserProfile>()
            .Where(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId)
            .Select(profile => profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var organization = await dbContext.Query<OrganizationAggregate>()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == new OrganizationId(query.Id), cancellationToken);

        var member = organization?.Members.SingleOrDefault(candidate => candidate.UserProfileId == profileId);
        return member is null || organization is null
            ? Error.NotFound("NOT_FOUND", "The organization was not found.")
            : new OrganizationSummary(
                organization.Id.Value,
                organization.Name,
                member.Roles,
                organization.Version,
                organization.AllowedEmailDomains,
                organization.AllowedTenantIds);
    }
}
