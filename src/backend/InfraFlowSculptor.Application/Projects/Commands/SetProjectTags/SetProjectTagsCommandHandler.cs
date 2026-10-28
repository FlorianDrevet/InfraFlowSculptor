using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Projects.Commands.SetProjectTags;

public sealed class SetProjectTagsCommandHandler(IIfsDbContext dbContext)
    : ICommandHandler<SetProjectTagsCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        SetProjectTagsCommand command,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.Tags)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(command.ProjectId), cancellationToken);
        if (project is null)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        if (project.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The project changed. Reload it before changing its tags.",
                new Dictionary<string, object> { ["currentVersion"] = project.Version });
        }

        var updated = project.SetTags(command.Tags);
        return updated.IsError
            ? updated.Errors
            : new ModelCommandOutcome($"Project tags replaced with {command.Tags.Count} tag(s).", []);
    }
}
