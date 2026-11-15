using ErrorOr;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.RevisionAggregate;

namespace InfraFlowSculptor.Domain.PublicationAggregate;

[AuditedObject(ObjectType.Publication, nameof(PublicationAggregate.Id), nameof(PublicationAggregate.OrganizationId))]
public sealed class PublicationAggregate : AggregateRoot<PublicationId>, IOrganizationOwned, IHasVersion
{
    private readonly List<PublicationTarget> targets = [];

    private PublicationAggregate()
    {
    }

    private PublicationAggregate(
        PublicationId id,
        OrganizationId organizationId,
        ProjectId projectId,
        RevisionId revisionId,
        int revisionNumber,
        UserKey author,
        DateTimeOffset requestedAt,
        string message,
        IEnumerable<PublicationTargetInput> targetInputs)
        : base(id)
    {
        OrganizationId = organizationId;
        ProjectId = projectId;
        RevisionId = revisionId;
        RevisionNumber = revisionNumber;
        AuthorTenantId = author.TenantId;
        AuthorObjectId = author.ObjectId;
        RequestedAt = requestedAt;
        Message = message;
        targets.AddRange(targetInputs.Select(input => new PublicationTarget(input)));
        State = PublicationState.Preparing;
    }

    public OrganizationId OrganizationId { get; private set; }

    public ProjectId ProjectId { get; private set; }

    public RevisionId RevisionId { get; private set; }

    public int RevisionNumber { get; private set; }

    public Guid AuthorTenantId { get; private set; }

    public Guid AuthorObjectId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? PreparedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public PublicationState State { get; private set; }

    public int Version { get; private set; } = 1;

    public IReadOnlyCollection<PublicationTarget> Targets => targets.AsReadOnly();

    public static ErrorOr<PublicationAggregate> Start(
        PublicationId id,
        OrganizationId organizationId,
        ProjectId projectId,
        RevisionId revisionId,
        int revisionNumber,
        UserKey author,
        DateTimeOffset requestedAt,
        string? message,
        IReadOnlyCollection<PublicationTargetInput> targetInputs)
    {
        if (id.Value == Guid.Empty
            || organizationId.Value == Guid.Empty
            || projectId.Value == Guid.Empty
            || revisionId.Value == Guid.Empty
            || revisionNumber < 1
            || author.TenantId == Guid.Empty
            || author.ObjectId == Guid.Empty
            || targetInputs is null
            || targetInputs.Count == 0)
        {
            return Error.Validation(PublicationValidationCodes.InvalidRequest, "La demande de publication est incomplète.");
        }

        var normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? $"IFS révision {revisionNumber}"
            : message.Trim();
        if (normalizedMessage.Length is < 1 or > 500)
        {
            return Error.Validation(PublicationValidationCodes.InvalidRequest, "Le message de publication doit contenir de 1 à 500 caractères.");
        }

        if (targetInputs.Any(input => input is null || !PublicationTarget.IsValid(input)))
        {
            return Error.Validation(PublicationValidationCodes.InvalidTarget, "Une destination de publication est invalide.");
        }

        if (targetInputs
            .Select(input => (input.GitConnectionId, RepositoryId: input.ProviderRepositoryId.Trim()))
            .DistinctBy(target => (target.GitConnectionId, target.RepositoryId), PublicationTargetKeyComparer.Instance)
            .Count() != targetInputs.Count)
        {
            return Error.Validation(PublicationValidationCodes.InvalidTarget, "Le même dépôt ne peut apparaître qu'une fois dans une opération.");
        }

        var destinationIds = targetInputs.SelectMany(input => input.Destinations).Select(destination => destination.DestinationId).ToArray();
        if (destinationIds.Distinct().Count() != destinationIds.Length)
        {
            return Error.Validation(PublicationValidationCodes.InvalidTarget, "Une destination ne peut appartenir qu'à un seul dépôt dans l'opération.");
        }

        return new PublicationAggregate(
            id,
            organizationId,
            projectId,
            revisionId,
            revisionNumber,
            author,
            requestedAt,
            normalizedMessage,
            targetInputs);
    }

    public ErrorOr<Success> MarkTargetPrepared(
        Guid targetId,
        string defaultBranch,
        string baseCommitSha,
        IEnumerable<PublicationTargetFileInput> files,
        bool requiresConfirmation,
        DateTimeOffset preparedAt)
    {
        if (State is not (PublicationState.Preparing or PublicationState.Prepared or PublicationState.NeedsConfirmation))
        {
            return Error.Conflict(PublicationValidationCodes.InvalidRequest, "La publication n'accepte plus de nouvelle préparation.");
        }

        var target = targets.SingleOrDefault(candidate => candidate.Id == targetId);
        if (target is null)
        {
            return Error.NotFound(PublicationValidationCodes.InvalidTarget, "La destination n'appartient pas à cette publication.");
        }

        try
        {
            target.MarkPrepared(defaultBranch, baseCommitSha, files, requiresConfirmation, preparedAt);
        }
        catch (ArgumentException)
        {
            return Error.Validation(PublicationValidationCodes.InvalidFiles, "La préparation contient des chemins ou empreintes invalides.");
        }
        catch (InvalidOperationException)
        {
            return Error.Conflict(PublicationValidationCodes.InvalidRequest, "La destination ne peut plus être préparée.");
        }

        UpdatePreparationState(preparedAt);

        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> ConfirmOwnership(Guid targetId, DateTimeOffset confirmedAt)
    {
        if (State != PublicationState.NeedsConfirmation)
        {
            return Error.Conflict(PublicationValidationCodes.InvalidRequest, "La publication n'attend pas de confirmation de propriété.");
        }

        var target = targets.SingleOrDefault(candidate => candidate.Id == targetId);
        if (target is null)
        {
            return Error.NotFound(PublicationValidationCodes.InvalidTarget, "La destination n'appartient pas à cette publication.");
        }

        try
        {
            target.ConfirmOwnership();
        }
        catch (InvalidOperationException)
        {
            return Error.Conflict(PublicationValidationCodes.InvalidRequest, "La destination ne demande pas de confirmation de propriété.");
        }

        UpdatePreparationState(confirmedAt);

        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> BeginWriting()
    {
        if (State != PublicationState.Prepared)
        {
            return Error.Conflict(PublicationValidationCodes.InvalidRequest, "La publication doit être préparée et confirmée avant l'écriture.");
        }

        if (targets.Any(target => target.State is not (PublicationTargetState.Prepared or PublicationTargetState.Failed))
            || targets.All(target => target.State == PublicationTargetState.Failed)
            || targets.Any(target => target.State == PublicationTargetState.Prepared && !target.OwnershipConfirmed))
        {
            return Error.Conflict(PublicationValidationCodes.InvalidRequest, "Toutes les destinations doivent être prêtes avant la première écriture.");
        }

        foreach (var target in targets)
        {
            if (target.State == PublicationTargetState.Prepared)
            {
                target.MarkPublishing();
            }
        }

        State = PublicationState.Publishing;
        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> ResetForRepreparation()
    {
        if (State != PublicationState.Publishing
            || targets.Any(target => target.CommitSha is not null || target.PullRequestId is not null))
        {
            return Error.Conflict(PublicationValidationCodes.InvalidRequest, "Une publication déjà écrite ne peut pas être entièrement préparée à nouveau.");
        }

        foreach (var target in targets)
        {
            target.ResetForPreparation();
        }

        State = PublicationState.Preparing;
        PreparedAt = null;
        CompletedAt = null;
        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> RecordPullRequest(
        Guid targetId,
        string commitSha,
        string pullRequestId,
        string pullRequestUrl,
        DateTimeOffset completedAt)
    {
        var target = targets.SingleOrDefault(candidate => candidate.Id == targetId);
        if (target is null)
        {
            return Error.NotFound(PublicationValidationCodes.InvalidTarget, "La destination n'appartient pas à cette publication.");
        }

        if (target.State == PublicationTargetState.Completed)
        {
            if (target.RecordPullRequest(commitSha, pullRequestId, pullRequestUrl, completedAt))
            {
                return Result.Success;
            }

            return Error.Conflict(PublicationValidationCodes.InvalidRequest, "Cette destination est déjà publiée avec un autre commit.");
        }

        if (State != PublicationState.Publishing)
        {
            return Error.Conflict(PublicationValidationCodes.InvalidRequest, "La publication n'est pas en cours d'écriture.");
        }

        try
        {
            target.RecordPullRequest(commitSha, pullRequestId, pullRequestUrl, completedAt);
        }
        catch (InvalidOperationException)
        {
            return Error.Validation(PublicationValidationCodes.InvalidPullRequest, "Le résultat de la pull request est invalide.");
        }

        CompleteIfAllTargetsTerminal(completedAt);
        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> RecordCommit(Guid targetId, string commitSha)
    {
        var target = targets.SingleOrDefault(candidate => candidate.Id == targetId);
        if (target is null)
        {
            return Error.NotFound(PublicationValidationCodes.InvalidTarget, "La destination n'appartient pas à cette publication.");
        }

        if (!target.RecordCommitSha(commitSha))
        {
            return Error.Conflict(PublicationValidationCodes.InvalidRequest, "Le commit de la destination est invalide ou a changé.");
        }

        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> RecordRecoveredCommit(
        Guid targetId,
        string commitSha,
        IReadOnlyCollection<PublicationTargetFileInput> recoveredManifestFiles)
    {
        var target = targets.SingleOrDefault(candidate => candidate.Id == targetId);
        if (target is null)
        {
            return Error.NotFound(PublicationValidationCodes.InvalidTarget, "La destination n'appartient pas à cette publication.");
        }

        if (!target.RecordRecoveredCommit(commitSha, recoveredManifestFiles))
        {
            return Error.Conflict(PublicationValidationCodes.InvalidRequest, "Le commit repris ou son manifeste ne correspond pas à la préparation.");
        }

        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> MarkTargetFailed(Guid targetId, string failureCode, DateTimeOffset failedAt)
    {
        var target = targets.SingleOrDefault(candidate => candidate.Id == targetId);
        if (target is null)
        {
            return Error.NotFound(PublicationValidationCodes.InvalidTarget, "La destination n'appartient pas à cette publication.");
        }

        try
        {
            target.MarkFailed(failureCode, failedAt);
        }
        catch (InvalidOperationException)
        {
            return Error.Conflict(PublicationValidationCodes.InvalidRequest, "La destination ne peut pas être marquée en échec.");
        }

        if (State == PublicationState.Publishing)
        {
            CompleteIfAllTargetsTerminal(failedAt);
        }
        else
        {
            UpdatePreparationState(failedAt);
        }
        Touch();
        return Result.Success;
    }

    public ErrorOr<Success> UpdatePullRequestState(
        Guid targetId,
        PublicationPullRequestState pullRequestState,
        string? mergeCommitSha,
        DateTimeOffset updatedAt)
    {
        var target = targets.SingleOrDefault(candidate => candidate.Id == targetId);
        if (target is null)
        {
            return Error.NotFound(PublicationValidationCodes.InvalidTarget, "La destination n'appartient pas à cette publication.");
        }

        try
        {
            target.UpdatePullRequestState(pullRequestState, mergeCommitSha, updatedAt);
        }
        catch (ArgumentException)
        {
            return Error.Validation(PublicationValidationCodes.InvalidPullRequest, "La pull request fusionnée doit fournir son commit de fusion.");
        }
        catch (InvalidOperationException)
        {
            return Error.Conflict(PublicationValidationCodes.InvalidPullRequest, "La pull request ne peut pas être suivie dans cet état.");
        }

        Touch();
        return Result.Success;
    }

    private void CompleteIfAllTargetsTerminal(DateTimeOffset completedAt)
    {
        if (targets.Any(candidate => candidate.State is not (PublicationTargetState.Completed or PublicationTargetState.Failed)))
        {
            return;
        }

        var succeeded = targets.Count(candidate => candidate.State == PublicationTargetState.Completed);
        State = succeeded switch
        {
            0 => PublicationState.Failed,
            var count when count == targets.Count => PublicationState.Completed,
            _ => PublicationState.PartiallyPublished
        };
        CompletedAt = completedAt;
    }

    private void UpdatePreparationState(DateTimeOffset preparedAt)
    {
        if (targets.Any(candidate => candidate.State is not (
                PublicationTargetState.Prepared
                or PublicationTargetState.NeedsConfirmation
                or PublicationTargetState.Failed)))
        {
            return;
        }

        if (targets.All(candidate => candidate.State == PublicationTargetState.Failed))
        {
            State = PublicationState.Failed;
            CompletedAt = preparedAt;
            return;
        }

        State = targets.Any(candidate => candidate.State == PublicationTargetState.NeedsConfirmation)
            ? PublicationState.NeedsConfirmation
            : PublicationState.Prepared;
        PreparedAt = preparedAt;
    }

    private void Touch() => Version = checked(Version + 1);

    private sealed class PublicationTargetKeyComparer : IEqualityComparer<(GitConnectionId GitConnectionId, string RepositoryId)>
    {
        public static PublicationTargetKeyComparer Instance { get; } = new();

        public bool Equals(
            (GitConnectionId GitConnectionId, string RepositoryId) x,
            (GitConnectionId GitConnectionId, string RepositoryId) y) =>
            x.GitConnectionId == y.GitConnectionId
            && string.Equals(x.RepositoryId, y.RepositoryId, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((GitConnectionId GitConnectionId, string RepositoryId) value) =>
            HashCode.Combine(value.GitConnectionId, StringComparer.OrdinalIgnoreCase.GetHashCode(value.RepositoryId));
    }
}
