using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Organizations;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Commands.ChangeMemberRoles;

public sealed class ChangeMemberRolesCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization)
    : ICommandHandler<ChangeMemberRolesCommand, ErrorOr<OrganizationMemberSummary>>
{
    public async ValueTask<ErrorOr<OrganizationMemberSummary>> Handle(
        ChangeMemberRolesCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId || organizationId.Value != command.OrganizationId)
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

        var memberProfileId = new UserProfileId(command.MemberId);
        var changed = organization.ChangeMemberRoles(memberProfileId, command.Roles);
        if (changed.IsError)
        {
            return changed.Errors;
        }

        var profile = await dbContext.Query<UserProfile>()
            .SingleOrDefaultAsync(candidate => candidate.Id == memberProfileId, cancellationToken);
        var member = organization.Members.Single(candidate => candidate.UserProfileId == memberProfileId);
        return profile is null
            ? Error.NotFound("NOT_FOUND", "The organization member was not found.")
            : new OrganizationMemberSummary(
                memberProfileId.Value,
                profile.DisplayName,
                profile.Email,
                member.Roles,
                organization.IsOutsideAccessRestrictions(profile));
    }
}
