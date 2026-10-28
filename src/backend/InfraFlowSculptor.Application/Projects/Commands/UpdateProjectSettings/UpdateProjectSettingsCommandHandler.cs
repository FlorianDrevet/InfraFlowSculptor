using System.Diagnostics.CodeAnalysis;
using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.UpdateProjectSettings;

[SuppressMessage("Performance", "CA1862", Justification = "EF Core translates lower() into PostgreSQL for the case-insensitive uniqueness check.")]
public sealed class UpdateProjectSettingsCommandHandler(IIfsDbContext dbContext)
    : Mediator.ICommandHandler<UpdateProjectSettingsCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        UpdateProjectSettingsCommand command,
        CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(command.ProjectId);
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.Tags)
            .SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict(
                "CONFLICT_VERSION",
                "The project changed. Reload it before updating its settings.",
                new Dictionary<string, object> { ["currentVersion"] = project.Version });
        }

        var normalizedName = command.Name.Trim().ToLower();
        var normalizedCode = command.Code.Trim().ToLower();
        var duplicate = await dbContext.Query<ProjectAggregate>()
            .IgnoreQueryFilters()
            .AnyAsync(candidate => candidate.OrganizationId == project.OrganizationId
                && candidate.Id != projectId
                && (candidate.Name.ToLower() == normalizedName
                    || candidate.Code.ToLower() == normalizedCode),
                cancellationToken);
        if (duplicate)
        {
            return Error.Conflict(
                "PROJECT_NAME_OR_CODE_DUPLICATE",
                "A project with this name or code already exists in the organization.");
        }

        if (command.CoordinatorUserProfileId is { } coordinatorId)
        {
            var coordinatorIsOrganizationMember = await dbContext.Query<OrganizationAggregate>()
                .Where(organization => organization.Id == project.OrganizationId)
                .SelectMany(organization => organization.Members)
                .AnyAsync(member => member.UserProfileId == new UserProfileId(coordinatorId), cancellationToken);
            if (!coordinatorIsOrganizationMember)
            {
                return Error.Validation(
                    "PROJECT_COORDINATOR_NOT_ORGANIZATION_MEMBER",
                    "The project coordinator must be a member of the organization.");
            }
        }

        var updated = project.UpdateSettings(
            command.Name,
            command.Code,
            command.Description,
            command.SystemTagsEnabled,
            command.CoordinatorUserProfileId is { } id ? new UserProfileId(id) : null,
            command.Tags,
            command.ConfirmImpact);
        return updated.IsError
            ? updated.Errors
            : new ModelCommandOutcome($"Project {project.Id.Value:D} settings updated.", []);
    }
}
