using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.SetComponentDefaultLogAnalytics;

public sealed class SetComponentDefaultLogAnalyticsCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization)
    : ICommandHandler<SetComponentDefaultLogAnalyticsCommand, ErrorOr<ModelCommandOutcome>>
{
    private const string WorkspaceType = "LogAnalyticsWorkspace";

    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        SetComponentDefaultLogAnalyticsCommand command,
        CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(
            dbContext, currentOrganization, command.ProjectId, command.ComponentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var (project, component) = loaded.Value;
        var version = ResourceCommandSupport.CheckComponentVersion(component, command.ExpectedVersion);
        if (version.IsError)
        {
            return version.Errors;
        }

        if (command.WorkspaceResourceId is { } workspaceResourceId)
        {
            var components = await dbContext.Query<ComponentAggregate>()
                .Where(candidate => candidate.ProjectId == new ProjectId(command.ProjectId))
                .ToArrayAsync(cancellationToken);
            var targetComponent = components.SingleOrDefault(candidate =>
                candidate.Resources.Any(resource => resource.Id == workspaceResourceId));
            var workspace = targetComponent?.Resources.Single(resource => resource.Id == workspaceResourceId);
            if (targetComponent is null || workspace is null)
            {
                return Error.NotFound("COMPONENT_DEFAULT_LOG_ANALYTICS_NOT_FOUND", "The Log Analytics resource was not found in this project.");
            }

            if (!string.Equals(workspace.Type, WorkspaceType, StringComparison.Ordinal))
            {
                return Error.Validation("COMPONENT_DEFAULT_LOG_ANALYTICS_TYPE_INVALID", "The default resource must be a Log Analytics workspace.");
            }

            if (component.Id != targetComponent.Id)
            {
                if (component.Mode == ComponentDeploymentMode.Single
                    && targetComponent.Mode == ComponentDeploymentMode.PerEnvironment)
                {
                    return Error.Validation("VAL-CMP-SENS", "A Single component cannot reference a PerEnvironment component.");
                }

                if (component.Mode == ComponentDeploymentMode.PerEnvironment
                    && targetComponent.Mode == ComponentDeploymentMode.PerEnvironment)
                {
                    var targetEnvironmentIds = ResourceCommandSupport.GetTargetIds(targetComponent, project).ToHashSet();
                    var uncoveredEnvironment = ResourceCommandSupport.GetTargetIds(component, project)
                        .Any(environmentId => !targetEnvironmentIds.Contains(environmentId));
                    if (uncoveredEnvironment)
                    {
                        return Error.Validation("VAL-CMP-ENVIRONNEMENTS", "The Log Analytics component must target every environment of this component.");
                    }
                }
            }
        }

        var updated = component.SetDefaultLogAnalyticsResource(command.WorkspaceResourceId);
        return updated.IsError
            ? updated.Errors
            : new ModelCommandOutcome($"Component {component.Code} default Log Analytics workspace updated.", []);
    }
}
