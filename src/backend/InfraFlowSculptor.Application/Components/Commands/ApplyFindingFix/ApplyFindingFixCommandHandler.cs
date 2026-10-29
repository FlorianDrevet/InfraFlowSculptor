using ErrorOr;
using InfraFlowSculptor.Application.Common.ModelCommands;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Components.Validation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common;
using Mediator;

namespace InfraFlowSculptor.Application.Components.Commands.ApplyFindingFix;

public sealed class ApplyFindingFixCommandHandler(
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    ProjectValidationService validationService,
    FindingFixRegistry fixes,
    IModelCommandExecutor executor)
    : ICommandHandler<ApplyFindingFixCommand, ErrorOr<ModelCommandExecution>>
{
    public async ValueTask<ErrorOr<ModelCommandExecution>> Handle(
        ApplyFindingFixCommand command,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await snapshotBuilder.BuildWithOrganizationNamingReservationsAsync(
            command.ProjectId,
            cancellationToken);
        if (snapshotResult.IsError)
        {
            return snapshotResult.Errors;
        }

        var snapshot = snapshotResult.Value;
        var catalog = catalogProvider.GetVersion(snapshot.Project.CatalogVersion);
        if (catalog is null)
        {
            return Error.Validation("PROJECT_CATALOG_VERSION_UNAVAILABLE", "The project's pinned catalog version is unavailable.");
        }

        var validation = await validationService.GetAsync(snapshot, catalog, cancellationToken);
        var finding = validation.Findings.SingleOrDefault(candidate =>
            string.Equals(candidate.Code, command.FindingCode, StringComparison.Ordinal)
            && candidate.Subject == new ObjectRef(command.SubjectType, command.SubjectId)
            && candidate.EnvironmentId == command.EnvironmentId
            && string.Equals(candidate.DetailKey, command.NormalizedDetailKey, StringComparison.Ordinal));
        if (finding is null || !string.Equals(finding.FixCode, command.FindingCode, StringComparison.Ordinal))
        {
            return Error.NotFound("VALIDATION_FIX_NOT_AVAILABLE", "This finding no longer has an available correction.");
        }

        if (finding.ObjectVersion is not { } currentVersion || currentVersion != command.ExpectedObjectVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The finding subject changed. Reload validation before applying its correction.");
        }

        if (!fixes.Supports(command.FindingCode))
        {
            return Error.Validation("VALIDATION_FIX_UNAVAILABLE", "This correction opens an editor and cannot be applied automatically.");
        }

        var modelCommand = fixes.Get(command.FindingCode).CreateCommand(new FindingFixContext(
            command.ProjectId,
            snapshot,
            catalog,
            validation,
            finding));
        if (modelCommand.IsError)
        {
            return modelCommand.Errors;
        }

        return await executor.ExecuteAsync([modelCommand.Value], ModelCommandMode.Apply, cancellationToken);
    }
}
