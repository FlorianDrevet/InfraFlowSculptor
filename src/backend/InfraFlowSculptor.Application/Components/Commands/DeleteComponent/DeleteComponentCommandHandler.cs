using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.DeleteComponent;

public sealed class DeleteComponentCommandHandler(IIfsDbContext dbContext)
    : ICommandHandler<DeleteComponentCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        DeleteComponentCommand command,
        CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(command.ProjectId);
        var component = await dbContext.Query<ComponentAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == new ComponentId(command.ComponentId)
                && candidate.ProjectId == projectId,
                cancellationToken);
        if (component is null)
        {
            return Error.NotFound("COMPONENT_NOT_FOUND", "The component was not found.");
        }

        if (component.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The component changed. Reload it before deleting.",
                new Dictionary<string, object> { ["currentVersion"] = component.Version });
        }

        var canDelete = component.EnsureCanBeDeleted();
        if (canDelete.IsError)
        {
            return canDelete.Errors;
        }

        dbContext.Query<ComponentAggregate>().Remove(component);
        return new ModelCommandOutcome($"Component {component.Code} deleted.", []);
    }
}
