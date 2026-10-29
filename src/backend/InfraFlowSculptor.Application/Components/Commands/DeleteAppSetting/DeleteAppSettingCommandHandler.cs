using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Commands;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.DeleteAppSetting;

public sealed class DeleteAppSettingCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization)
    : ICommandHandler<DeleteAppSettingCommand, ErrorOr<ModelCommandOutcome>>
{
    public async ValueTask<ErrorOr<ModelCommandOutcome>> Handle(DeleteAppSettingCommand command, CancellationToken cancellationToken)
    {
        var loaded = await ResourceCommandSupport.LoadAsync(dbContext, currentOrganization,
            command.ProjectId, command.ComponentId, cancellationToken);
        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        var setting = await dbContext.Query<AppSettingAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == command.AppSettingId
                && candidate.ProjectId == new ProjectId(command.ProjectId)
                && candidate.ComponentId == new ComponentId(command.ComponentId)
                && candidate.ApplicationResourceId == command.ApplicationResourceId,
                cancellationToken);
        if (setting is null)
        {
            return Error.NotFound("APP_SETTING_NOT_FOUND", "The application setting was not found.");
        }

        if (setting.Version != command.ExpectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The application setting changed. Reload it before deleting.",
                new Dictionary<string, object> { ["currentVersion"] = setting.Version });
        }

        dbContext.Query<AppSettingAggregate>().Remove(setting);
        return new ModelCommandOutcome($"Application setting {setting.Name} deleted.", []);
    }
}
