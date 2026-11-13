using System.Text.RegularExpressions;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.RevisionAggregate;

[AuditedObject(ObjectType.Revision, nameof(RevisionAggregate.Id), nameof(RevisionAggregate.OrganizationId))]
public sealed partial class RevisionAggregate : AggregateRoot<RevisionId>, IOrganizationOwned
{
    private readonly List<RevisionFinding> findings = [];
    private readonly List<RevisionFile> files = [];
    private readonly List<RevisionChange> changes = [];

    private RevisionAggregate()
    {
    }

    private RevisionAggregate(
        RevisionId id,
        OrganizationId organizationId,
        ProjectId projectId,
        int number,
        UserKey author,
        DateTimeOffset createdAt,
        int modelVersion,
        string catalogVersion,
        string infrastructureLanguage,
        string ciPlatform,
        string inputSha256)
        : base(id)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);

        ArgumentException.ThrowIfNullOrWhiteSpace(catalogVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(infrastructureLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(ciPlatform);
        if (!Sha256Pattern().IsMatch(inputSha256))
        {
            throw new ArgumentException("The frozen input hash must be a SHA-256 value.", nameof(inputSha256));
        }

        OrganizationId = organizationId;
        ProjectId = projectId;
        Number = number;
        AuthorTenantId = author.TenantId;
        AuthorObjectId = author.ObjectId;
        CreatedAt = createdAt;
        ModelVersion = modelVersion;
        CatalogVersion = catalogVersion;
        InfrastructureLanguage = infrastructureLanguage;
        CiPlatform = ciPlatform;
        InputSha256 = inputSha256;
        State = RevisionState.Queued;
    }

    public OrganizationId OrganizationId { get; private set; }

    public ProjectId ProjectId { get; private set; }

    public int Number { get; private set; }

    public Guid AuthorTenantId { get; private set; }

    public Guid AuthorObjectId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public int ModelVersion { get; private set; }

    public string CatalogVersion { get; private set; } = string.Empty;

    public string InfrastructureLanguage { get; private set; } = string.Empty;

    public string CiPlatform { get; private set; } = string.Empty;

    public string InputSha256 { get; private set; } = string.Empty;

    public RevisionState State { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public string? FailureCode { get; private set; }

    public string DeploymentPlanJson { get; private set; } = string.Empty;

    public IReadOnlyCollection<RevisionFinding> Findings => findings.AsReadOnly();

    public IReadOnlyCollection<RevisionFile> Files => files.AsReadOnly();

    public IReadOnlyCollection<RevisionChange> Changes => changes.AsReadOnly();

    public static RevisionAggregate Queue(
        RevisionId id,
        OrganizationId organizationId,
        ProjectId projectId,
        int number,
        UserKey author,
        DateTimeOffset createdAt,
        int modelVersion,
        string catalogVersion,
        string infrastructureLanguage,
        string ciPlatform,
        string inputSha256) =>
        new(
            id,
            organizationId,
            projectId,
            number,
            author,
            createdAt,
            modelVersion,
            catalogVersion,
            infrastructureLanguage,
            ciPlatform,
            inputSha256);

    public void MarkGenerating(DateTimeOffset startedAt)
    {
        if (State != RevisionState.Queued && State != RevisionState.Generating)
        {
            throw new InvalidOperationException("Only a queued revision can start generation.");
        }

        State = RevisionState.Generating;
        StartedAt ??= startedAt;
        FailureCode = null;
    }

    public void MarkGenerated(
        string deploymentPlanJson,
        IEnumerable<RevisionFinding> generatedFindings,
        IEnumerable<RevisionFile> generatedFiles,
        IEnumerable<RevisionChange> generatedChanges,
        DateTimeOffset completedAt)
    {
        if (State != RevisionState.Generating)
        {
            throw new InvalidOperationException("Only a generating revision can complete.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentPlanJson);
        ArgumentNullException.ThrowIfNull(generatedFindings);
        ArgumentNullException.ThrowIfNull(generatedFiles);
        ArgumentNullException.ThrowIfNull(generatedChanges);

        var findingList = generatedFindings.ToArray();
        if (findingList.Any(finding => finding.Severity is not (
                RevisionFindingSeverity.ErrorAtPublish
                or RevisionFindingSeverity.Warning
                or RevisionFindingSeverity.Information)))
        {
            throw new ArgumentException("A generated revision can contain only non-blocking findings.", nameof(generatedFindings));
        }

        var fileList = generatedFiles.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
        if (fileList.Any(file => string.IsNullOrWhiteSpace(file.RelativePath)
                || file.RelativePath.StartsWith('/')
                || file.RelativePath.Contains('\\')
                || file.RelativePath.Split('/').Any(segment => segment is "" or "." or "..")
                || !Sha256Pattern().IsMatch(file.Sha256)
                || file.Size < 0)
            || fileList.Select(file => file.RelativePath).Distinct(StringComparer.Ordinal).Count() != fileList.Length)
        {
            throw new ArgumentException("Generated files must have unique, safe paths and valid hashes.", nameof(generatedFiles));
        }

        DeploymentPlanJson = deploymentPlanJson;
        findings.AddRange(findingList);
        files.AddRange(fileList);
        changes.AddRange(generatedChanges);
        CompletedAt = completedAt;
        State = RevisionState.Generated;
    }

    public void MarkFailed(string failureCode, DateTimeOffset completedAt)
    {
        if (State != RevisionState.Queued && State != RevisionState.Generating)
        {
            throw new InvalidOperationException("Only a queued or generating revision can fail.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        FailureCode = failureCode;
        CompletedAt = completedAt;
        State = RevisionState.Failed;
    }

    public void MarkPublished(DateTimeOffset publishedAt)
    {
        if (State != RevisionState.Generated)
        {
            throw new InvalidOperationException("Only a generated revision can be published.");
        }

        PublishedAt ??= publishedAt;
    }

    public void PurgeFiles()
    {
        if (PublishedAt is not null)
        {
            throw new InvalidOperationException("Published revision files must be retained.");
        }

        files.Clear();
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
