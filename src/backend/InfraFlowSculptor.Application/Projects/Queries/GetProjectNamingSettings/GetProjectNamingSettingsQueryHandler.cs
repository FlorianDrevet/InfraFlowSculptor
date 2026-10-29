using ErrorOr;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Naming;
using InfraFlowSculptor.Application.Projects;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Queries.GetProjectNamingSettings;

public sealed class GetProjectNamingSettingsQueryHandler(IIfsDbContext dbContext)
    : IQueryHandler<GetProjectNamingSettingsQuery, ErrorOr<ProjectNamingSettings>>
{
    public async ValueTask<ErrorOr<ProjectNamingSettings>> Handle(
        GetProjectNamingSettingsQuery query,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.NamingOverrides)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(query.ProjectId), cancellationToken);
        return project is null
            ? Error.NotFound("NOT_FOUND", "The project was not found.")
            : new ProjectNamingSettings(
                project.Id.Value,
                project.Version,
                project.NamingPreset,
                project.DefaultNameTemplate,
                project.ResourceGroupNameTemplate,
                project.FirstPublishedAt is not null,
                project.NamingOverrides
                    .OrderBy(value => value.Type, StringComparer.OrdinalIgnoreCase)
                    .Select(value => new NamingOverrideValue(value.Type, value.Template, value.Abbreviation))
                    .ToArray());
    }
}
