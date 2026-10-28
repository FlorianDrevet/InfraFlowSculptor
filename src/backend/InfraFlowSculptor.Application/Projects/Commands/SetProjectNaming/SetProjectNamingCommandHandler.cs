using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Naming;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.SetProjectNaming;

public sealed class SetProjectNamingCommandHandler(IIfsDbContext dbContext, ICatalogProvider catalogProvider)
    : ICommandHandler<SetProjectNamingCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        SetProjectNamingCommand command,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.NamingOverrides)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(command.ProjectId), cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before changing its naming settings.",
                new Dictionary<string, object> { ["currentVersion"] = project.Version });
        }

        var catalog = catalogProvider.GetVersion(project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var validTypes = NamingOverrideTypeValidator.Validate(command.TypeOverrides, catalog);
        if (validTypes.IsError)
        {
            return validTypes.Errors;
        }

        var configured = project.ConfigureNaming(command.Preset, command.DefaultTemplate, command.ResourceGroupTemplate);
        if (configured.IsError)
        {
            return configured.Errors;
        }

        var overridesByType = command.TypeOverrides.ToDictionary(value => value.Type.Trim(), StringComparer.OrdinalIgnoreCase);
        foreach (var existing in project.NamingOverrides.ToArray())
        {
            if (!overridesByType.ContainsKey(existing.Type))
            {
                var cleared = project.ConfigureTypeNamingOverride(existing.Type, null, null);
                if (cleared.IsError)
                {
                    return cleared.Errors;
                }
            }
        }

        foreach (var value in command.TypeOverrides)
        {
            var updated = project.ConfigureTypeNamingOverride(value.Type, value.Template, value.Abbreviation);
            if (updated.IsError)
            {
                return updated.Errors;
            }
        }

        return new ModelCommandOutcome($"Project {project.Id.Value:D} naming settings updated.", []);
    }
}
