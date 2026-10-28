using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.DeleteProject;

public sealed class DeleteProjectCommandHandler(IIfsDbContext dbContext, TimeProvider clock)
    : ICommandHandler<DeleteProjectCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        DeleteProjectCommand command,
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
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before deleting it.",
                new Dictionary<string, object> { ["currentVersion"] = project.Version });
        }

        if (!string.Equals(project.Name, command.NameConfirmation, StringComparison.Ordinal))
        {
            return Error.Validation("PROJECT_DELETE_NAME_MISMATCH", "Enter the project name exactly to confirm deletion.");
        }

        var deleted = project.SoftDelete(clock.GetUtcNow());
        return deleted.IsError
            ? deleted.Errors
            : new ModelCommandOutcome($"Project {project.Id.Value:D} is recoverable for 30 days.", []);
    }
}
