using System.Security.Cryptography;
using System.Text;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Observability;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.RevisionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Emitters.InstallKit;
using InfraFlowSculptor.Engine.Plan;
using InfraFlowSculptor.Engine.Validation;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed partial class GenerateRevisionJobHandler(
    IIfsDbContext dbContext,
    ICatalogProvider catalogProvider,
    EngineFacade engineFacade,
    InfrastructureEmitterRegistry infrastructureEmitters,
    PipelineEmitterRegistry pipelineEmitters,
    InstallKitPlatformEmitterRegistry installKitEmitters,
    RevisionOutputChecker outputChecker,
    IRevisionBlobStore blobStore,
    TimeProvider clock,
    ILogger<GenerateRevisionJobHandler> logger) : JobHandler<GenerateRevisionJob>
{
    private const string OutputCheckFailureCode = "OUTPUT_CHECK_FAILED";
    private const string CatalogVersionUnavailableCode = "CATALOG_VERSION_UNAVAILABLE";
    private const string GenerationBlockedCode = "GENERATION_BLOCKED";

    public override async Task HandleAsync(
        GenerateRevisionJob job,
        JobExecutionContext context,
        CancellationToken cancellationToken)
    {
        var revisionId = new RevisionId(job.RevisionId);
        var revision = await dbContext.Query<RevisionAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == revisionId
                && candidate.OrganizationId == context.OrganizationId,
                cancellationToken);
        if (revision is null || revision.State is RevisionState.Generated or RevisionState.Failed)
        {
            return;
        }

        var input = await dbContext.Query<RevisionInputEntity>()
            .SingleOrDefaultAsync(candidate => candidate.RevisionId == revisionId, cancellationToken);
        if (input is null)
        {
            throw new InvalidDataException("The frozen revision input was not found.");
        }

        ValidateFrozenInput(revision, input);
        var snapshot = RevisionInputJsonSerializer.Deserialize(input.Snapshot);
        if (snapshot.Project.ModelVersion != input.ModelVersion
            || !string.Equals(snapshot.Project.CatalogVersion, input.CatalogVersion, StringComparison.Ordinal)
            || !string.Equals(snapshot.Project.InfrastructureLanguage, input.InfrastructureLanguage, StringComparison.Ordinal)
            || !string.Equals(snapshot.Project.CiPlatform, input.CiPlatform, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The frozen revision metadata does not match its snapshot.");
        }

        revision.MarkGenerating(clock.GetUtcNow());

        var catalog = catalogProvider.GetVersion(input.CatalogVersion);
        if (catalog is null)
        {
            revision.MarkFailed(CatalogVersionUnavailableCode, clock.GetUtcNow());
            return;
        }

        var evaluation = engineFacade.Evaluate(snapshot, catalog);
        if (evaluation.Validation.Findings.Any(finding => finding.Severity == FindingSeverity.Error))
        {
            revision.MarkFailed(GenerationBlockedCode, clock.GetUtcNow());
            return;
        }

        if (!Enum.TryParse<IacLanguage>(input.InfrastructureLanguage, ignoreCase: false, out var language)
            || !Enum.IsDefined(language)
            || !Enum.TryParse<CiPlatform>(input.CiPlatform, ignoreCase: false, out var platform)
            || !Enum.IsDefined(platform))
        {
            revision.MarkFailed(GenerationBlockedCode, clock.GetUtcNow());
            return;
        }

        var generatedFiles = infrastructureEmitters.Get(new EmitterKey(language))
            .Emit(evaluation.Plan, catalog)
            .Concat(pipelineEmitters.Get(platform).Emit(evaluation.Plan, catalog))
            .Concat(installKitEmitters.Get(platform).Emit(evaluation.Plan, catalog))
            .Concat(InstallKitEmitter.Emit(evaluation.Plan))
            .Append(ReadmeEmitter.Emit(evaluation.Plan))
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
        if (generatedFiles.Select(file => file.RelativePath).Distinct(StringComparer.Ordinal).Count() != generatedFiles.Length)
        {
            throw new InvalidOperationException("The generation emitters produced duplicate file paths.");
        }

        try
        {
            await outputChecker.CheckAsync(generatedFiles, language, evaluation.Plan.Pins, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            revision.MarkFailed(OutputCheckFailureCode, clock.GetUtcNow());
            IfsTelemetry.RecordOutputCheckFailure(context.OrganizationId);
            var pseudonymizedOrganizationId = IfsTelemetry.Pseudonymize(context.OrganizationId.Value);
            OutputCheckFailed(logger, revision.Id.Value, pseudonymizedOrganizationId);
            return;
        }

        var revisionFiles = new List<RevisionFile>(generatedFiles.Length);
        foreach (var file in generatedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = Encoding.UTF8.GetBytes(file.Content);
            var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            await blobStore.EnsureFileAsync(sha256, content, cancellationToken);
            revisionFiles.Add(new RevisionFile(file.RelativePath, sha256, content.LongLength));
        }

        var previousRevision = await dbContext.Query<RevisionAggregate>()
            .Where(candidate => candidate.ProjectId == revision.ProjectId
                && candidate.Number < revision.Number
                && candidate.State == RevisionState.Generated)
            .OrderByDescending(candidate => candidate.Number)
            .FirstOrDefaultAsync(cancellationToken);
        var deploymentPlanJson = DeploymentPlanJsonSerializer.Serialize(evaluation.Plan);
        var changes = RevisionPlanDiffFactory.Create(previousRevision?.DeploymentPlanJson, deploymentPlanJson)
            .Concat(RevisionChangeFactory.Create(previousRevision?.Files.ToArray(), revisionFiles))
            .ToArray();
        var findings = evaluation.Validation.Findings.Select(ToRevisionFinding).ToArray();
        revision.MarkGenerated(
            deploymentPlanJson,
            findings,
            revisionFiles,
            changes,
            clock.GetUtcNow());
    }

    private static void ValidateFrozenInput(RevisionAggregate revision, RevisionInputEntity input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input.Snapshot);
        var actualHash = Convert.ToHexString(SHA256.HashData(inputBytes)).ToLowerInvariant();
        if (!string.Equals(actualHash, input.Sha256, StringComparison.Ordinal)
            || !string.Equals(input.Sha256, revision.InputSha256, StringComparison.Ordinal)
            || input.ModelVersion != revision.ModelVersion
            || !string.Equals(input.CatalogVersion, revision.CatalogVersion, StringComparison.Ordinal)
            || !string.Equals(input.InfrastructureLanguage, revision.InfrastructureLanguage, StringComparison.Ordinal)
            || !string.Equals(input.CiPlatform, revision.CiPlatform, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The frozen revision input failed its integrity check.");
        }
    }

    private static RevisionFinding ToRevisionFinding(ValidationFinding finding) =>
        new(
            finding.Code,
            finding.Severity switch
            {
                FindingSeverity.ErrorAtPublish => RevisionFindingSeverity.ErrorAtPublish,
                FindingSeverity.Warning => RevisionFindingSeverity.Warning,
                FindingSeverity.Info => RevisionFindingSeverity.Information,
                _ => throw new InvalidOperationException("A generated revision contains a blocking finding.")
            },
            finding.MessageFr,
            finding.MessageEn,
            finding.Subject.Type.ToString(),
            finding.Subject.Id,
            finding.EnvironmentId);

    [LoggerMessage(
        EventId = 2401,
        Level = LogLevel.Error,
        Message = "OutputCheckFailed for revision {RevisionId} in organization {OrganizationId}")]
    private static partial void OutputCheckFailed(ILogger logger, Guid revisionId, string organizationId);
}
