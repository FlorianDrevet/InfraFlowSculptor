using System.Diagnostics.CodeAnalysis;
using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Plans;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Engine.Model;
using InfraFlowSculptor.Engine.Naming;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Wizard;

[SuppressMessage("Performance", "CA1862", Justification = "EF Core translates lower() into PostgreSQL for the case-insensitive uniqueness check.")]
public sealed class CreateProjectFromWizardCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICurrentUser currentUser,
    IPlanLimitGuard planLimitGuard,
    ICatalogProvider catalogProvider)
    : ICommandHandler<CreateProjectFromWizardCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        CreateProjectFromWizardCommand command,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The organization was not found.");
        }

        var limit = planLimitGuard.EnsureCanCreate(organizationId, LimitKind.Project);
        if (limit.IsError)
        {
            return limit.Errors;
        }

        var catalogVersion = catalogProvider.GetVersion(command.CatalogVersion);
        if (catalogVersion is null || !string.Equals(catalogVersion.Status, "published", StringComparison.Ordinal))
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_INVALID", "The project must pin a published catalog version.");
        }

        if (command.Environments.Count == 0)
        {
            return Error.Validation("PROJECT_ENVIRONMENTS_REQUIRED", "A project must have at least one environment.");
        }

        var duplicate = await dbContext.Query<ProjectAggregate>()
            .IgnoreQueryFilters()
            .AnyAsync(project => project.OrganizationId == organizationId
                && (project.Name.ToLower() == command.Name.Trim().ToLower()
                    || project.Code.ToLower() == command.Code.Trim().ToLower()),
                cancellationToken);
        if (duplicate)
        {
            return Error.Conflict("PROJECT_NAME_OR_CODE_DUPLICATE", "A project with this name or code already exists in the organization.");
        }

        var creator = await dbContext.Query<UserProfile>()
            .SingleOrDefaultAsync(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId, cancellationToken);
        if (creator is null)
        {
            return Error.NotFound("NOT_FOUND", "The current user profile was not found.");
        }

        var snapshot = new ModelSnapshot(
            new ProjectSnapshot(command.Name, command.Code, command.Description),
            command.Environments.Select(environment => new EnvironmentSnapshot(environment.Code, environment.Region)).ToArray(),
            new NamingConventionSnapshot(
                command.NamingPreset,
                command.DefaultNameTemplate,
                command.ResourceGroupNameTemplate),
            command.Tags,
            []);
        var preview = NamingEngine.Preview(snapshot);
        if (preview.Issues.Count > 0)
        {
            return preview.Issues
                .Select(issue => Error.Validation(issue.Code, issue.Message))
                .ToArray();
        }

        var projectResult = ProjectAggregate.Create(
            new ProjectId(command.ProjectId),
            organizationId,
            command.Name,
            command.Code,
            command.TenantId,
            command.Description,
            catalogVersion.Version,
            creator.Id);
        if (projectResult.IsError)
        {
            return projectResult.Errors;
        }

        var project = projectResult.Value;
        var naming = project.ConfigureNaming(
            command.NamingPreset,
            command.DefaultNameTemplate,
            command.ResourceGroupNameTemplate);
        if (naming.IsError)
        {
            return naming.Errors;
        }

        var publishing = project.ConfigurePublishing(command.PublishingPreset);
        if (publishing.IsError)
        {
            return publishing.Errors;
        }

        var tags = project.SetTags(command.Tags);
        if (tags.IsError)
        {
            return tags.Errors;
        }

        dbContext.Query<ProjectAggregate>().Add(project);
        foreach (var (environment, index) in command.Environments.Select((value, index) => (value, index)))
        {
            if (!catalogVersion.Regions.Any(region =>
                    string.Equals(region.Name, environment.Region.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return Error.Validation("ENVIRONMENT_REGION_INVALID", "The selected region is not present in the project's pinned catalog.");
            }

            var created = project.AddEnvironment(
                environment.Id,
                environment.Name,
                environment.Code,
                environment.SubscriptionId,
                environment.Region,
                index + 1,
                environment.IsProtected,
                environment.Approvers,
                [],
                environment.Description);
            if (created.IsError)
            {
                return created.Errors;
            }
        }

        var draft = await dbContext.Query<ProjectWizardDraft>()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId
                && candidate.UserProfileId == creator.Id, cancellationToken);
        if (draft is not null)
        {
            dbContext.Query<ProjectWizardDraft>().Remove(draft);
        }

        return new ModelCommandOutcome(
            $"Project {project.Id.Value:D} created with {project.Environments.Count} environment(s).",
            []);
    }
}
