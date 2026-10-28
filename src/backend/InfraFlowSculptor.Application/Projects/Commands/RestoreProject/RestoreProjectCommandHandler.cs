using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.RestoreProject;

public sealed class RestoreProjectCommandHandler(IIfsDbContext dbContext, TimeProvider clock)
    : ICommandHandler<RestoreProjectCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        RestoreProjectCommand command,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Query<ProjectAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(command.ProjectId), cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before restoring it.",
                new Dictionary<string, object> { ["currentVersion"] = project.Version });
        }

        var restored = project.Restore(clock.GetUtcNow());
        return restored.IsError
            ? restored.Errors
            : new ModelCommandOutcome($"Project {project.Id.Value:D} restored.", []);
    }
}
