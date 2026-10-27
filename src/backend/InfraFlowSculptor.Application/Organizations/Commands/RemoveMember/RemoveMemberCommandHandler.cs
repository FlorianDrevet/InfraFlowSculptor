using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Commands.RemoveMember;

public sealed class RemoveMemberCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    IProjectOwnershipCheck projectOwnershipCheck)
    : ICommandHandler<RemoveMemberCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(
        RemoveMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId || organizationId.Value != command.OrganizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var organization = await dbContext.Query<OrganizationAggregate>()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);
        var memberProfileId = new UserProfileId(command.MemberId);
        if (organization is null || organization.Members.All(member => member.UserProfileId != memberProfileId))
        {
            return Error.NotFound("NOT_FOUND", "The organization member was not found.");
        }

        if (await projectOwnershipCheck.WouldRemovingMemberRemoveLastProjectOwnerAsync(
                organizationId,
                memberProfileId,
                cancellationToken))
        {
            return Error.Conflict("RG-ORG-11", "Assign another project owner before removing this member.");
        }

        return organization.RemoveMember(memberProfileId);
    }
}
