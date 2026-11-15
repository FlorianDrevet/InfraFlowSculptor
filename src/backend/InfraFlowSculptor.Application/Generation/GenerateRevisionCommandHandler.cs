using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Components;
using InfraFlowSculptor.Application.Components.Validation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.RevisionAggregate;
using InfraFlowSculptor.Engine.Validation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Generation;

public sealed class GenerateRevisionCommandHandler(
    IIfsDbContext dbContext,
    ModelSnapshotBuilder snapshotBuilder,
    ICatalogProvider catalogProvider,
    ProjectValidationService validationService,
    ICurrentOrganization currentOrganization,
    ICurrentUser currentUser,
    IJobDispatcher jobDispatcher,
    TimeProvider clock)
    : ICommandHandler<GenerateRevisionCommand, ErrorOr<RevisionGenerationResult>>
{
    public async ValueTask<ErrorOr<RevisionGenerationResult>> Handle(
        GenerateRevisionCommand command,
        CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(command.ProjectId);
        if (!await dbContext.LockProjectForUpdateAsync(projectId, cancellationToken))
        {
            return Error.NotFound("PROJECT_NOT_FOUND", "The project was not found.");
        }

        dbContext.ClearTrackedChanges();
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
            return Error.Validation(
                "PROJECT_CATALOG_VERSION_UNAVAILABLE",
                "The project's pinned catalog version is unavailable.");
        }

        var validation = await validationService.GetAsync(snapshot, catalog, cancellationToken);
        if (validation.Findings.Any(finding => finding.Severity == FindingSeverity.Error))
        {
            return Error.Validation("GENERATION_BLOCKED", "The project contains blocking validation findings.");
        }

        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("PROJECT_NOT_FOUND", "The project was not found.");
        }

        var snapshotJson = RevisionInputJsonSerializer.Serialize(snapshot);
        var snapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson))).ToLowerInvariant();
        var lastRevision = await dbContext.Query<RevisionAggregate>()
            .Where(revision => revision.ProjectId == projectId)
            .OrderByDescending(revision => revision.Number)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastRevision is not null
            && lastRevision.ModelVersion == snapshot.Project.ModelVersion
            && string.Equals(lastRevision.CatalogVersion, snapshot.Project.CatalogVersion, StringComparison.Ordinal)
            && string.Equals(lastRevision.InfrastructureLanguage, snapshot.Project.InfrastructureLanguage, StringComparison.Ordinal)
            && string.Equals(lastRevision.CiPlatform, snapshot.Project.CiPlatform, StringComparison.Ordinal)
            && string.Equals(lastRevision.InputSha256, snapshotHash, StringComparison.Ordinal))
        {
            int? queuePosition = lastRevision.State == RevisionState.Queued
                ? await GetQueuePositionAsync(lastRevision, organizationId, cancellationToken)
                : null;
            return new RevisionGenerationResult(
                lastRevision.Id.Value,
                lastRevision.Number,
                lastRevision.State,
                queuePosition,
                IsUnchanged: true);
        }

        var createdAt = clock.GetUtcNow();
        var revisionId = RevisionId.New(clock);
        var number = (lastRevision?.Number ?? 0) + 1;
        var queuePositionForNewRevision = await dbContext.Query<RevisionAggregate>()
            .CountAsync(revision => revision.OrganizationId == organizationId
                && revision.State == RevisionState.Queued, cancellationToken) + 1;
        var revision = RevisionAggregate.Queue(
            revisionId,
            organizationId,
            projectId,
            number,
            currentUser.Key,
            createdAt,
            snapshot.Project.ModelVersion,
            snapshot.Project.CatalogVersion,
            snapshot.Project.InfrastructureLanguage,
            snapshot.Project.CiPlatform,
            snapshotHash);
        dbContext.Query<RevisionAggregate>().Add(revision);
        dbContext.Query<RevisionInputEntity>().Add(new RevisionInputEntity
        {
            RevisionId = revisionId,
            ModelVersion = snapshot.Project.ModelVersion,
            CatalogVersion = snapshot.Project.CatalogVersion,
            InfrastructureLanguage = snapshot.Project.InfrastructureLanguage,
            CiPlatform = snapshot.Project.CiPlatform,
            Snapshot = snapshotJson,
            Sha256 = snapshotHash
        });
        jobDispatcher.Enqueue(JobQueue.Generation, organizationId, new GenerateRevisionJob(revisionId.Value));

        return new RevisionGenerationResult(
            revisionId.Value,
            number,
            RevisionState.Queued,
            queuePositionForNewRevision,
            IsUnchanged: false);
    }

    private async Task<int> GetQueuePositionAsync(
        RevisionAggregate target,
        OrganizationId organizationId,
        CancellationToken cancellationToken) =>
        await dbContext.Query<RevisionAggregate>()
            .CountAsync(revision => revision.OrganizationId == organizationId
                && revision.State == RevisionState.Queued
                && (revision.CreatedAt < target.CreatedAt
                    || (revision.CreatedAt == target.CreatedAt && revision.Number <= target.Number)),
                cancellationToken);
}
