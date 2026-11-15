using System.Text.RegularExpressions;
using InfraFlowSculptor.Domain.GitConnectionAggregate;

namespace InfraFlowSculptor.Domain.PublicationAggregate;

public sealed partial class PublicationTarget
{
    private readonly List<PublicationTargetDestination> destinations = [];
    private readonly List<PublicationTargetFile> files = [];

    private PublicationTarget()
    {
    }

    internal PublicationTarget(PublicationTargetInput input)
    {
        Id = Guid.CreateVersion7();
        GitConnectionId = input.GitConnectionId;
        ProviderRepositoryId = input.ProviderRepositoryId.Trim();
        RepositoryName = input.RepositoryName.Trim();
        TargetBranch = input.TargetBranch.Trim();
        SourceBranch = input.SourceBranch.Trim();
        destinations.AddRange(input.Destinations.Select(destination => new PublicationTargetDestination(
            destination.DestinationId,
            destination.BasePath.Trim().Trim('/'))));
        files.AddRange(input.Files.Select(file => new PublicationTargetFile(
            file.DestinationId,
            file.RepositoryPath,
            file.Sha256,
            file.Action,
            file.ExistingSha256)));
    }

    public Guid Id { get; private set; }

    public GitConnectionId GitConnectionId { get; private set; }

    public string ProviderRepositoryId { get; private set; } = string.Empty;

    public string RepositoryName { get; private set; } = string.Empty;

    public string DefaultBranch { get; private set; } = string.Empty;

    public string BaseCommitSha { get; private set; } = string.Empty;

    public string TargetBranch { get; private set; } = string.Empty;

    public string SourceBranch { get; private set; } = string.Empty;

    public string? CommitSha { get; private set; }

    public string? PullRequestId { get; private set; }

    public string? PullRequestUrl { get; private set; }

    public string? MergeCommitSha { get; private set; }

    public PublicationTargetState State { get; private set; } = PublicationTargetState.Preparing;

    public PublicationPullRequestState PullRequestState { get; private set; } = PublicationPullRequestState.NotCreated;

    public bool RequiresConfirmation { get; private set; }

    public bool OwnershipConfirmed { get; private set; }

    public string? FailureCode { get; private set; }

    public DateTimeOffset? PreparedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyCollection<PublicationTargetDestination> Destinations => destinations.AsReadOnly();

    public IReadOnlyCollection<PublicationTargetFile> Files => files.AsReadOnly();

    internal static bool IsValid(PublicationTargetInput input)
    {
        if (input.GitConnectionId.Value == Guid.Empty
            || string.IsNullOrWhiteSpace(input.ProviderRepositoryId)
            || input.ProviderRepositoryId.Length > 300
            || string.IsNullOrWhiteSpace(input.RepositoryName)
            || input.RepositoryName.Length > 300
            || !IsSafeBranch(input.TargetBranch)
            || !IsSafeBranch(input.SourceBranch)
            || input.Destinations is null
            || input.Destinations.Count == 0
            || input.Destinations.Any(destination => destination is null
                || destination.DestinationId == Guid.Empty
                || destination.BasePath is null
                || (!string.IsNullOrEmpty(destination.BasePath) && string.IsNullOrWhiteSpace(destination.BasePath))
                || destination.BasePath.Length > 200
                || destination.BasePath.StartsWith('/')
                || destination.BasePath.Contains('\\')
                || destination.BasePath.Split('/').Any(segment => segment is "." or "..")))
        {
            return false;
        }

        var destinationIds = input.Destinations.Select(destination => destination.DestinationId).ToHashSet();
        return input.Destinations.Select(destination => destination.DestinationId).Distinct().Count()
                == input.Destinations.Count
            && input.Files is not null
            && input.Files.All(file => file is not null
                && destinationIds.Contains(file.DestinationId)
                && IsSafeRepositoryPath(file.RepositoryPath)
                && file.Sha256 is not null
                && GitSha256Pattern().IsMatch(file.Sha256)
                && file.Action == PublicationFileAction.Add
                && file.ExistingSha256 is null)
            && input.Files.Select(file => file.RepositoryPath).Distinct(StringComparer.Ordinal).Count() == input.Files.Count;
    }

    internal void MarkPrepared(
        string defaultBranch,
        string baseCommitSha,
        IEnumerable<PublicationTargetFileInput> preparedFiles,
        bool requiresConfirmation,
        DateTimeOffset preparedAt)
    {
        if (State is not (PublicationTargetState.Preparing or PublicationTargetState.Prepared or PublicationTargetState.NeedsConfirmation))
        {
            throw new InvalidOperationException("Only an unstarted publication target can be prepared.");
        }

        if (!IsSafeBranch(defaultBranch)
            || !GitShaPattern().IsMatch(baseCommitSha)
            || preparedFiles is null)
        {
            throw new ArgumentException("The publication target preparation is invalid.");
        }

        var fileList = preparedFiles.OrderBy(file => file.RepositoryPath, StringComparer.Ordinal).ToArray();
        if (fileList.Any(file => !IsSafeRepositoryPath(file.RepositoryPath)
                || !destinations.Any(destination => destination.DestinationId == file.DestinationId)
                || !IsValidFileChange(file))
            || fileList.Select(file => file.RepositoryPath).Distinct(StringComparer.Ordinal).Count() != fileList.Length)
        {
            throw new ArgumentException("Publication files must have safe unique paths and valid hashes.", nameof(preparedFiles));
        }

        DefaultBranch = defaultBranch.Trim();
        BaseCommitSha = baseCommitSha;
        files.Clear();
        files.AddRange(fileList.Select(file => new PublicationTargetFile(
            file.DestinationId,
            file.RepositoryPath,
            file.Sha256,
            file.Action,
            file.ExistingSha256)));
        RequiresConfirmation = requiresConfirmation;
        OwnershipConfirmed = !requiresConfirmation;
        PreparedAt = preparedAt;
        FailureCode = null;
        State = requiresConfirmation ? PublicationTargetState.NeedsConfirmation : PublicationTargetState.Prepared;
    }

    internal void ConfirmOwnership()
    {
        if (State != PublicationTargetState.NeedsConfirmation || !RequiresConfirmation)
        {
            throw new InvalidOperationException("This publication target has no ownership conflict to confirm.");
        }

        OwnershipConfirmed = true;
        State = PublicationTargetState.Prepared;
    }

    internal void MarkPublishing()
    {
        if (State != PublicationTargetState.Prepared || !OwnershipConfirmed)
        {
            throw new InvalidOperationException("Only a prepared publication target can be written.");
        }

        State = PublicationTargetState.Publishing;
    }

    internal void ResetForPreparation()
    {
        if (CommitSha is not null || PullRequestId is not null || State == PublicationTargetState.Completed)
        {
            throw new InvalidOperationException("A target with a remote commit cannot be prepared again in the same operation.");
        }

        DefaultBranch = string.Empty;
        BaseCommitSha = string.Empty;
        files.RemoveAll(file => file.Action == PublicationFileAction.Delete);
        RequiresConfirmation = false;
        OwnershipConfirmed = false;
        FailureCode = null;
        PreparedAt = null;
        CompletedAt = null;
        State = PublicationTargetState.Preparing;
    }

    internal bool RecordPullRequest(
        string commitSha,
        string pullRequestId,
        string pullRequestUrl,
        DateTimeOffset completedAt)
    {
        if (State == PublicationTargetState.Completed)
        {
            return string.Equals(CommitSha, commitSha, StringComparison.Ordinal)
                && string.Equals(PullRequestId, pullRequestId, StringComparison.Ordinal);
        }

        if (State != PublicationTargetState.Publishing
            || !GitShaPattern().IsMatch(commitSha)
            || (CommitSha is not null && !string.Equals(CommitSha, commitSha, StringComparison.Ordinal))
            || string.IsNullOrWhiteSpace(pullRequestId)
            || pullRequestId.Length > 200
            || !Uri.TryCreate(pullRequestUrl, UriKind.Absolute, out var pullRequestUri)
            || pullRequestUri.Scheme is not ("https" or "http"))
        {
            throw new InvalidOperationException("The publication pull request result is invalid.");
        }

        CommitSha = commitSha;
        PullRequestId = pullRequestId;
        PullRequestUrl = pullRequestUri.ToString();
        PullRequestState = PublicationPullRequestState.Open;
        State = PublicationTargetState.Completed;
        CompletedAt = completedAt;
        FailureCode = null;
        return true;
    }

    internal bool RecordCommitSha(string commitSha)
    {
        if (!GitShaPattern().IsMatch(commitSha))
        {
            return false;
        }

        if (CommitSha is not null)
        {
            return string.Equals(CommitSha, commitSha, StringComparison.Ordinal);
        }

        if (State != PublicationTargetState.Publishing)
        {
            return false;
        }

        CommitSha = commitSha;
        return true;
    }

    internal bool RecordRecoveredCommit(
        string commitSha,
        IReadOnlyCollection<PublicationTargetFileInput> recoveredManifestFiles)
    {
        if (State != PublicationTargetState.Publishing
            || !GitShaPattern().IsMatch(commitSha)
            || (CommitSha is not null && !string.Equals(CommitSha, commitSha, StringComparison.OrdinalIgnoreCase))
            || recoveredManifestFiles is null)
        {
            return false;
        }

        var manifestFiles = files.Where(file => IsManifestFilePath(file.RepositoryPath)).ToArray();
        var recoveredFiles = recoveredManifestFiles.ToArray();
        if (manifestFiles.Length == 0
            || recoveredFiles.Length != manifestFiles.Length
            || recoveredFiles.Any(file => file is null
                || !IsManifestFilePath(file.RepositoryPath)
                || !IsValidFileChange(file)
                || file.Sha256 is null)
            || recoveredFiles.Select(file => (file.DestinationId, file.RepositoryPath)).Distinct().Count() != recoveredFiles.Length)
        {
            return false;
        }

        var recoveredByIdentity = recoveredFiles.ToDictionary(
            file => (file.DestinationId, file.RepositoryPath));
        foreach (var currentFile in manifestFiles)
        {
            if (!recoveredByIdentity.TryGetValue((currentFile.DestinationId, currentFile.RepositoryPath), out var recoveredFile)
                || recoveredFile.Action != currentFile.Action
                || !string.Equals(recoveredFile.ExistingSha256, currentFile.ExistingSha256, StringComparison.Ordinal))
            {
                return false;
            }
        }

        foreach (var currentFile in manifestFiles)
        {
            currentFile.ReplaceContentHash(recoveredByIdentity[(currentFile.DestinationId, currentFile.RepositoryPath)].Sha256!);
        }

        CommitSha = commitSha;
        return true;
    }

    internal void MarkFailed(string failureCode, DateTimeOffset failedAt)
    {
        if (State is PublicationTargetState.Completed or PublicationTargetState.Failed
            || string.IsNullOrWhiteSpace(failureCode))
        {
            throw new InvalidOperationException("This publication target cannot be marked as failed.");
        }

        FailureCode = failureCode;
        CompletedAt = failedAt;
        State = PublicationTargetState.Failed;
    }

    internal void UpdatePullRequestState(
        PublicationPullRequestState pullRequestState,
        string? mergeCommitSha,
        DateTimeOffset updatedAt)
    {
        if (State != PublicationTargetState.Completed || PullRequestState == PublicationPullRequestState.NotCreated)
        {
            throw new InvalidOperationException("Only a created pull request can be tracked.");
        }

        if (pullRequestState == PublicationPullRequestState.Merged
            && (mergeCommitSha is null || !GitShaPattern().IsMatch(mergeCommitSha)))
        {
            throw new ArgumentException("A merged pull request must have a valid merge commit SHA.", nameof(mergeCommitSha));
        }

        PullRequestState = pullRequestState;
        MergeCommitSha = pullRequestState == PublicationPullRequestState.Merged ? mergeCommitSha : null;
        LastTrackedAt = updatedAt;
    }

    public DateTimeOffset? LastTrackedAt { get; private set; }

    private static bool IsSafeRepositoryPath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.Length <= 500
        && !path.StartsWith('/')
        && !path.Contains('\\')
        && path.Split('/').All(segment => segment.Length > 0 && segment is not ("." or ".."));

    private static bool IsManifestFilePath(string path) =>
        string.Equals(path, ".ifs/manifest.json", StringComparison.Ordinal)
        || path.EndsWith("/.ifs/manifest.json", StringComparison.Ordinal);

    private static bool IsValidFileChange(PublicationTargetFileInput file) => file.Action switch
    {
        PublicationFileAction.Add => file.Sha256 is not null
            && GitSha256Pattern().IsMatch(file.Sha256)
            && file.ExistingSha256 is null,
        PublicationFileAction.Update => file.Sha256 is not null
            && GitSha256Pattern().IsMatch(file.Sha256)
            && file.ExistingSha256 is not null
            && GitSha256Pattern().IsMatch(file.ExistingSha256),
        PublicationFileAction.Delete => file.Sha256 is null
            && file.ExistingSha256 is not null
            && GitSha256Pattern().IsMatch(file.ExistingSha256),
        PublicationFileAction.Unchanged => file.Sha256 is not null
            && GitSha256Pattern().IsMatch(file.Sha256)
            && string.Equals(file.Sha256, file.ExistingSha256, StringComparison.Ordinal),
        _ => false
    };

    private static bool IsSafeBranch(string? branch) =>
        !string.IsNullOrWhiteSpace(branch)
        && branch.Length <= 240
        && !branch.StartsWith('/')
        && !branch.EndsWith('/')
        && !branch.EndsWith('.')
        && !branch.Contains("//", StringComparison.Ordinal)
        && !branch.Contains('\\')
        && !branch.Contains("..", StringComparison.Ordinal)
        && branch.IndexOfAny([' ', '\t', '\n', '\r', '~', '^', ':', '?', '*', '[', '\\']) < 0
        && !branch.Contains("@{", StringComparison.Ordinal)
        && !branch.EndsWith(".lock", StringComparison.Ordinal)
        && !branch.Split('/').Any(segment => segment.Length == 0 || segment.StartsWith('.') || segment.EndsWith('.'));

    [GeneratedRegex("\\A[0-9a-f]{40}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex GitShaPattern();

    [GeneratedRegex("\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex GitSha256Pattern();
}
