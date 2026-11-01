using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Engine.Links;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.CreateAppSetting;

public sealed class ForceNonSecretAppSettingCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    InfraFlowSculptor.Catalog.ICatalogProvider catalogProvider,
    ModelSnapshotBuilder snapshotBuilder,
    WiringEngine wiringEngine)
    : ICommandHandler<ForceNonSecretAppSettingCommand, ErrorOr<ModelCommandOutcome>>
{
    public ValueTask<ErrorOr<ModelCommandOutcome>> Handle(ForceNonSecretAppSettingCommand command, CancellationToken cancellationToken) =>
        AppSettingCommandSupport.CreateAsync(dbContext, currentOrganization, catalogProvider, snapshotBuilder, wiringEngine,
            command.ProjectId, command.ComponentId, command.ApplicationResourceId, command.AppSettingId,
            command.Definition, cancellationToken);
}
