using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Engine.Links;
using InfraFlowSculptor.Catalog;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.UpdateAppSetting;

public sealed class ForceNonSecretAppSettingUpdateCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICatalogProvider catalogProvider,
    ModelSnapshotBuilder snapshotBuilder,
    WiringEngine wiringEngine)
    : ICommandHandler<ForceNonSecretAppSettingUpdateCommand, ErrorOr<ModelCommandOutcome>>
{
    public ValueTask<ErrorOr<ModelCommandOutcome>> Handle(
        ForceNonSecretAppSettingUpdateCommand command,
        CancellationToken cancellationToken) =>
        new UpdateAppSettingCommandHandler(dbContext, currentOrganization, catalogProvider, snapshotBuilder, wiringEngine).Handle(
            new UpdateAppSettingCommand(
                command.ProjectId,
                command.ComponentId,
                command.ApplicationResourceId,
                command.AppSettingId,
                command.Definition,
                command.ExpectedVersion),
            cancellationToken);
}
