using ErrorOr;
using InfraFlowSculptor.Application.Common.Errors;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components.Validation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Engine.Validation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands.AcknowledgeFinding;

public sealed class AcknowledgeFindingCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICurrentUser currentUser,
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    ProjectValidationService validationService,
    TimeProvider clock)
    : ICommandHandler<AcknowledgeFindingCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(
        AcknowledgeFindingCommand command,
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
            && candidate.Subject.Type == command.SubjectType
            && candidate.Subject.Id == command.SubjectId
            && candidate.EnvironmentId == command.EnvironmentId
            && string.Equals(candidate.DetailKey, command.NormalizedDetailKey, StringComparison.Ordinal));
        if (finding is null)
        {
            return Error.NotFound("VALIDATION_FINDING_NOT_FOUND", "The finding no longer exists for the current model.");
        }

        if (finding.Severity != FindingSeverity.Warning)
        {
            return Error.Validation("VALIDATION_FINDING_NOT_ACKNOWLEDGEABLE", "Only a warning can be acknowledged.");
        }

        if (finding.ObjectVersion is not { } objectVersion)
        {
            return Error.Validation("VALIDATION_FINDING_NOT_ACKNOWLEDGEABLE", "This finding does not have a version that can be acknowledged.");
        }

        if (objectVersion != command.ExpectedObjectVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The finding subject changed. Reload validation before acknowledging it.");
        }

        var project = await dbContext.Query<ProjectAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(command.ProjectId), cancellationToken);
        if (project is null || currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("PROJECT_NOT_FOUND", "The project was not found.");
        }

        var profile = await dbContext.Query<UserProfile>()
            .SingleOrDefaultAsync(candidate => candidate.TenantId == currentUser.Key.TenantId
                && candidate.ObjectId == currentUser.Key.ObjectId, cancellationToken);
        if (profile is null)
        {
            return Error.NotFound("USER_PROFILE_NOT_FOUND", "The current user profile was not found.");
        }

        var alreadyAcknowledged = await dbContext.Query<FindingAcknowledgmentAggregate>()
            .AnyAsync(candidate => candidate.ProjectId == project.Id
                && candidate.FindingCode == finding.Code
                && candidate.SubjectType == finding.Subject.Type
                && candidate.SubjectId == finding.Subject.Id
                && candidate.EnvironmentId == finding.EnvironmentId
                && candidate.DetailKey == finding.DetailKey
                && candidate.ObjectVersion == objectVersion, cancellationToken);
        if (alreadyAcknowledged)
        {
            return Error.Conflict(
                FindingAcknowledgmentErrors.AlreadyAcknowledgedCode,
                FindingAcknowledgmentErrors.AlreadyAcknowledgedMessage);
        }

        var acknowledgment = FindingAcknowledgmentAggregate.Create(
            Guid.CreateVersion7(clock.GetUtcNow()),
            project.Id,
            organizationId,
            finding.Code,
            finding.DetailKey,
            finding.Subject.Type,
            finding.Subject.Id,
            finding.EnvironmentId,
            objectVersion,
            command.Comment,
            profile.Id,
            clock.GetUtcNow());
        if (acknowledgment.IsError)
        {
            return acknowledgment.Errors;
        }

        dbContext.Query<FindingAcknowledgmentAggregate>().Add(acknowledgment.Value);
        return Result.Success;
    }
}
