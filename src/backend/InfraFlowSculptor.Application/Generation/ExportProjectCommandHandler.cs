using ErrorOr;
using InfraFlowSculptor.Application.Components;
using Mediator;

namespace InfraFlowSculptor.Application.Generation;

public sealed class ExportProjectCommandHandler(
    ModelSnapshotBuilder snapshotBuilder,
    IProjectExportStore exportStore,
    TimeProvider clock)
    : ICommandHandler<ExportProjectCommand, ErrorOr<ProjectExportLink>>
{
    public async ValueTask<ErrorOr<ProjectExportLink>> Handle(
        ExportProjectCommand command,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await snapshotBuilder.BuildAsync(command.ProjectId, cancellationToken);
        if (snapshotResult.IsError)
        {
            return snapshotResult.Errors;
        }

        var expiresAt = clock.GetUtcNow().Add(ProjectExportPolicy.LinkLifetime);
        var content = RevisionInputJsonSerializer.SerializeToUtf8Bytes(snapshotResult.Value);
        var token = await exportStore.SaveAsync(content, expiresAt, cancellationToken);
        return new ProjectExportLink(token, expiresAt);
    }
}
