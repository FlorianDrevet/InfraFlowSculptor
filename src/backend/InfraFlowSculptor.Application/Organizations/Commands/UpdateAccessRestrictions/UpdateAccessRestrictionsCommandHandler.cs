using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Commands.UpdateAccessRestrictions;

public sealed class UpdateAccessRestrictionsCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization)
    : ICommandHandler<UpdateAccessRestrictionsCommand, ErrorOr<OrganizationSummary>>
{
    public async ValueTask<ErrorOr<OrganizationSummary>> Handle(
        UpdateAccessRestrictionsCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId || organizationId.Value != command.OrganizationId)
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
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);
        var member = organization?.Members.SingleOrDefault(candidate => candidate.UserProfileId == profileId);
        if (organization is null || member is null)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var updated = organization.UpdateAccessRestrictions(
            command.AllowedEmailDomains,
            command.AllowedTenantIds);
        if (updated.IsError)
        {
            return updated.Errors;
        }

        return new OrganizationSummary(
            organization.Id.Value,
            organization.Name,
            member.Roles,
            organization.Version + 1,
            organization.AllowedEmailDomains,
            organization.AllowedTenantIds);
    }
}
