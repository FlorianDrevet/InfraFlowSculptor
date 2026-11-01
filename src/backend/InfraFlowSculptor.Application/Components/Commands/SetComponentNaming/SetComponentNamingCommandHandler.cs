using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Naming;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.SetComponentNaming;

public sealed class SetComponentNamingCommandHandler(IIfsDbContext dbContext, ICatalogProvider catalogProvider)
    : ICommandHandler<SetComponentNamingCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        SetComponentNamingCommand command,
        CancellationToken cancellationToken)
    {
        var component = await dbContext.Query<ComponentAggregate>()
            .Include(candidate => candidate.NamingOverrides)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ComponentId(command.ComponentId)
                && candidate.ProjectId == new ProjectId(command.ProjectId), cancellationToken);
        if (component is null)
        {
            return Error.NotFound("NOT_FOUND", "The component was not found.");
        }

        var project = await dbContext.Query<ProjectAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(command.ProjectId), cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
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

        if (component.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The component changed. Reload it before changing its naming settings.",
                new Dictionary<string, object> { ["currentVersion"] = component.Version });
        }

        var defaultTemplate = component.ConfigureDefaultNameTemplate(command.DefaultTemplate);
        if (defaultTemplate.IsError)
        {
            return defaultTemplate.Errors;
        }

        var overridesByType = command.TypeOverrides.ToDictionary(value => value.Type.Trim(), StringComparer.OrdinalIgnoreCase);
        foreach (var existing in component.NamingOverrides.ToArray())
        {
            if (!overridesByType.ContainsKey(existing.Type))
            {
                var cleared = component.ConfigureTypeNamingOverride(existing.Type, null, null);
                if (cleared.IsError)
                {
                    return cleared.Errors;
                }
            }
        }

        foreach (var value in command.TypeOverrides)
        {
            var updated = component.ConfigureTypeNamingOverride(value.Type, value.Template, value.Abbreviation);
            if (updated.IsError)
            {
                return updated.Errors;
            }
        }

        return new ModelCommandOutcome($"Component {component.Code} naming settings updated.", []);
    }
}
