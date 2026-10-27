using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Queries.GetEffectivePermissions;

public sealed class GetEffectivePermissionsQueryHandler(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentOrganization currentOrganization,
    IOrganizationRoleAuthorizer organizationRoleAuthorizer)
    : IQueryHandler<GetEffectivePermissionsQuery, ErrorOr<ProjectEffectivePermissions>>
{
    public async ValueTask<ErrorOr<ProjectEffectivePermissions>> Handle(
        GetEffectivePermissionsQuery query,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var project = await dbContext.Query<ProjectAggregate>()
            .AsNoTracking()
            .Include(candidate => candidate.RoleAssignments)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(query.ProjectId), cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        UserProfile? member;
        if (query.MemberId is { } requestedMemberId)
        {
            member = await dbContext.Query<UserProfile>()
                .SingleOrDefaultAsync(profile => profile.Id == new UserProfileId(requestedMemberId), cancellationToken);
        }
        else
        {
            member = await dbContext.Query<UserProfile>()
                .SingleOrDefaultAsync(profile => profile.TenantId == currentUser.Key.TenantId
                    && profile.ObjectId == currentUser.Key.ObjectId,
                    cancellationToken);
        }

        if (member is null)
        {
            return Error.NotFound("NOT_FOUND", "The organization member was not found.");
        }

        var isOrganizationMember = await dbContext.Query<OrganizationAggregate>()
            .Where(candidate => candidate.Id == organizationId)
            .SelectMany(candidate => candidate.Members)
            .AnyAsync(candidate => candidate.UserProfileId == member.Id, cancellationToken);
        if (!isOrganizationMember)
        {
            return Error.NotFound("NOT_FOUND", "The organization member was not found.");
        }

        var assignments = project.RoleAssignments
            .Where(assignment => assignment.Principal == member.Id)
            .OrderBy(assignment => assignment.Role)
            .ThenBy(assignment => assignment.Scope)
            .Select(assignment => new ProjectRoleAssignmentSummary(
                member.Id.Value,
                assignment.Role,
                assignment.Scope,
                project.Version,
                ProjectRoleSources.Direct))
            .ToArray();

        var permissions = project.EffectivePermissions(member.Id).ToHashSet(StringComparer.Ordinal);
        if (!permissions.Contains(Permission.ProjectRead)
            && await organizationRoleAuthorizer.HasRoleAsync(
                member.UserKey,
                organizationId,
                OrganizationRole.Administrator,
                cancellationToken))
        {
            permissions.Add(Permission.ProjectRead);
        }

        return new ProjectEffectivePermissions(
            member.Id.Value,
            project.Version,
            permissions.Order(StringComparer.Ordinal).ToArray(),
            assignments);
    }
}
