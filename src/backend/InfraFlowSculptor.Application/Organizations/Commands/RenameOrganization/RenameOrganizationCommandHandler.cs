using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Commands.RenameOrganization;

public sealed class RenameOrganizationCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICurrentUser currentUser)
    : ICommandHandler<RenameOrganizationCommand, ErrorOr<OrganizationSummary>>
{
    public async ValueTask<ErrorOr<OrganizationSummary>> Handle(
        RenameOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId || organizationId.Value != command.Id)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var organization = await dbContext.Query<OrganizationAggregate>()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);

        if (organization is null)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var renamed = organization.Rename(command.Name);
        if (renamed.IsError)
        {
            return renamed.Errors;
        }

        var profileId = await dbContext.Query<UserProfile>()
            .Where(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId)
            .Select(profile => profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var roles = organization.Members.Single(member => member.UserProfileId == profileId).Roles;
        return new OrganizationSummary(
            organization.Id.Value,
            organization.Name,
            roles,
            organization.Version + 1,
            organization.AllowedEmailDomains,
            organization.AllowedTenantIds);
    }
}
