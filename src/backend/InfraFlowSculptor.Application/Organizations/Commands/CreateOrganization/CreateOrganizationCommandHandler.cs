using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Plans;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Organizations.Commands.CreateOrganization;

public sealed class CreateOrganizationCommandHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    IPlanLimitGuard planLimitGuard,
    TimeProvider clock)
    : ICommandHandler<CreateOrganizationCommand, ErrorOr<OrganizationSummary>>
{
    public async ValueTask<ErrorOr<OrganizationSummary>> Handle(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        var limit = planLimitGuard.EnsureCanCreate(null, LimitKind.Organization);
        if (limit.IsError)
        {
            return limit.Errors;
        }

        var profile = await dbContext.Query<UserProfile>()
            .SingleOrDefaultAsync(
                candidate => candidate.TenantId == currentUser.Key.TenantId
                    && candidate.ObjectId == currentUser.Key.ObjectId,
                cancellationToken);

        if (profile is null)
        {
            return Error.NotFound("USER_PROFILE_NOT_FOUND", "The authenticated user profile was not found.");
        }

        var created = OrganizationAggregate.Create(
            OrganizationId.New(clock),
            command.Name,
            profile.Id);
        if (created.IsError)
        {
            return created.Errors;
        }

        dbContext.Query<OrganizationAggregate>().Add(created.Value);
        var adminMember = created.Value.Members.Single();
        return new OrganizationSummary(
            created.Value.Id.Value,
            created.Value.Name,
            adminMember.Roles,
            created.Value.Version);
    }
}
