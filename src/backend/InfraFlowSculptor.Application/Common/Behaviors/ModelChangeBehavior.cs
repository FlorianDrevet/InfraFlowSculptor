using System.Reflection;
using ErrorOr;
using InfraFlowSculptor.Application.Common.Extensibility;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Common.Behaviors;

public sealed class ModelChangeBehavior<TMessage, TResponse>(IIfsDbContext dbContext)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : IErrorOr
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(message, cancellationToken);
        if (response.IsError
            || message is not IModelCommand command
            || typeof(TMessage).GetCustomAttribute<ModelChangeAttribute>() is null)
        {
            return response;
        }

        var projectId = new ProjectId(command.Workspace.Id);
        var projects = dbContext.Query<ProjectAggregate>();
        var project = projects.Local.SingleOrDefault(candidate => candidate.Id == projectId)
            ?? await projects.SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        project?.RecordModelChange();
        return response;
    }
}
