using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Application.Common.Secrets;
using InfraFlowSculptor.Application.Generation;
using InfraFlowSculptor.Application.GitConnections;
using InfraFlowSculptor.Application.Publications;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Security;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.PublicationAggregate;
using InfraFlowSculptor.Domain.RevisionAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;
using Publication = InfraFlowSculptor.Domain.PublicationAggregate.PublicationAggregate;
using GitConnection = InfraFlowSculptor.Domain.GitConnectionAggregate.GitConnectionAggregate;

namespace InfraFlowSculptor.Worker.Jobs;

public sealed partial class PublicationOperationProcessor(
    IIfsDbContext dbContext,
    IRevisionBlobStore blobStore,
    GitProviderRegistry providers,
    ISecretStore secretStore,
    IProjectAuthorizer projectAuthorizer,
    IJobDispatcher jobDispatcher,
    TimeProvider clock,
    ILogger<PublicationOperationProcessor> logger)
{
    private const string ManifestPath = ".ifs/manifest.json";
    private const string ManifestSchema = "ifs-manifest/v1";
    private const int MaximumDescriptionFiles = 40;
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task PrepareAsync(PreparePublicationJob job, JobExecutionContext context, CancellationToken cancellationToken)
    {
        var publication = await LoadPublicationAsync(job.PublicationId, context.OrganizationId, cancellationToken);
        if (publication is null || publication.State != PublicationState.Preparing)
        {
            return;
        }

        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.PublicationPlan.Destinations)
            .Include(candidate => candidate.PublicationPlan.Assignments)
            .SingleOrDefaultAsync(candidate => candidate.Id == publication.ProjectId, cancellationToken);
        var revision = await dbContext.Query<RevisionAggregate>()
            .Include(candidate => candidate.Findings)
            .Include(candidate => candidate.Changes)
            .SingleOrDefaultAsync(candidate => candidate.Id == publication.RevisionId, cancellationToken);
        if (project is null || revision is null || revision.State != RevisionState.Generated)
        {
            foreach (var target in publication.Targets.Where(target => target.State == PublicationTargetState.Preparing))
            {
                _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.RevisionNotGenerated, clock.GetUtcNow());
            }

            return;
        }

        var sourceDirectoriesByDestination = await GetSourceDirectoriesByDestinationAsync(project, cancellationToken);
        foreach (var target in publication.Targets.Where(target => target.State == PublicationTargetState.Preparing))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var connection = await LoadConnectionAsync(target.GitConnectionId, cancellationToken);
                if (connection is null || !IsRepositoryAccessible(connection, target, project.Id))
                {
                    _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.InvalidTarget, clock.GetUtcNow());
                    continue;
                }

                var provider = providers.Get(connection.ProviderKind);
                var accessToken = await GetAccessTokenAsync(connection, cancellationToken);
                var baseBranch = await provider.GetBranchAsync(
                    connection,
                    target.ProviderRepositoryId,
                    target.TargetBranch,
                    accessToken,
                    cancellationToken);
                if (baseBranch is null)
                {
                    _ = publication.MarkTargetFailed(target.Id, "TARGET_BRANCH_MISSING", clock.GetUtcNow());
                    continue;
                }

                var treePaths = await provider.GetTreePathsAsync(
                    connection,
                    target.ProviderRepositoryId,
                    target.TargetBranch,
                    accessToken,
                    cancellationToken);
                if (!HasRequiredSourceDirectories(target, treePaths, sourceDirectoriesByDestination))
                {
                    _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.ExtensionMissing, clock.GetUtcNow());
                    continue;
                }

                var priorTarget = await FindPriorTargetAsync(publication.ProjectId, target, cancellationToken);
                var preparedFiles = await PrepareTargetFilesAsync(
                    publication,
                    project,
                    target,
                    baseBranch.Name,
                    provider,
                    connection,
                    accessToken,
                    treePaths,
                    priorTarget,
                    cancellationToken);
                var requiresConfirmation = preparedFiles.RequiresConfirmation;
                var stored = publication.MarkTargetPrepared(
                    target.Id,
                    baseBranch.Name,
                    baseBranch.CommitSha,
                    preparedFiles.Files,
                    requiresConfirmation,
                    clock.GetUtcNow());
                if (stored.IsError)
                {
                    _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.InvalidFiles, clock.GetUtcNow());
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException exception) when (IsTransientProviderFailure(exception))
            {
                throw;
            }
            catch (Exception exception)
            {
                PublicationTargetFailed(logger, publication.Id.Value, target.Id, exception.GetType().Name);
                _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.ProviderFailure, clock.GetUtcNow());
            }
        }
    }

    public async Task WriteAsync(WritePublicationJob job, JobExecutionContext context, CancellationToken cancellationToken)
    {
        var publication = await LoadPublicationAsync(job.PublicationId, context.OrganizationId, cancellationToken);
        if (publication is null || publication.State != PublicationState.Publishing)
        {
            return;
        }

        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.Environments)
            .Include(candidate => candidate.PublicationPlan.Destinations)
            .Include(candidate => candidate.PublicationPlan.Assignments)
            .SingleOrDefaultAsync(candidate => candidate.Id == publication.ProjectId, cancellationToken);
        var revision = await dbContext.Query<RevisionAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == publication.RevisionId, cancellationToken);
        if (project is null || revision is null)
        {
            foreach (var target in publication.Targets.Where(target => target.State == PublicationTargetState.Publishing))
            {
                _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.InvalidRequest, clock.GetUtcNow());
            }

            return;
        }

        var changedBase = await FindChangedBaseAsync(publication, cancellationToken);
        if (changedBase is { } changedTarget)
        {
            if (publication.Targets.Any(target => target.CommitSha is not null || target.PullRequestId is not null))
            {
                foreach (var target in publication.Targets.Where(target => target.State == PublicationTargetState.Publishing))
                {
                    _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.BranchChanged, clock.GetUtcNow());
                }

                return;
            }

            _ = changedTarget;
            if (publication.ResetForRepreparation().IsError)
            {
                foreach (var target in publication.Targets.Where(target => target.State == PublicationTargetState.Publishing))
                {
                    _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.BranchChanged, clock.GetUtcNow());
                }

                return;
            }

            jobDispatcher.Enqueue(JobQueue.Publication, context.OrganizationId, new PreparePublicationJob(publication.Id.Value));
            return;
        }

        var author = new UserKey(publication.AuthorTenantId, publication.AuthorObjectId);
        var authorName = await GetAuthorNameAsync(author, cancellationToken);
        var blocked = false;
        foreach (var target in publication.Targets.Where(target => target.State == PublicationTargetState.Publishing))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (blocked || !await HasPublishPermissionAsync(author, publication, cancellationToken))
            {
                blocked = true;
                FailUnstartedTargets(publication, PublicationValidationCodes.PermissionRevoked);
                break;
            }

            try
            {
                var connection = await LoadConnectionAsync(target.GitConnectionId, cancellationToken);
                if (connection is null || !IsRepositoryAccessible(connection, target, project.Id))
                {
                    _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.InvalidTarget, clock.GetUtcNow());
                    continue;
                }

                var provider = providers.Get(connection.ProviderKind);
                var accessToken = await GetAccessTokenAsync(connection, cancellationToken);
                var branch = await provider.GetBranchAsync(
                    connection,
                    target.ProviderRepositoryId,
                    target.TargetBranch,
                    accessToken,
                    cancellationToken);
                if (branch is null || !string.Equals(branch.CommitSha, target.BaseCommitSha, StringComparison.OrdinalIgnoreCase))
                {
                    if (publication.Targets.Any(candidate => candidate.CommitSha is not null || candidate.PullRequestId is not null))
                    {
                        _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.BranchChanged, clock.GetUtcNow());
                        continue;
                    }

                    _ = publication.ResetForRepreparation();
                    jobDispatcher.Enqueue(JobQueue.Publication, context.OrganizationId, new PreparePublicationJob(publication.Id.Value));
                    return;
                }

                var sourceBranch = await provider.GetBranchAsync(
                    connection,
                    target.ProviderRepositoryId,
                    target.SourceBranch,
                    accessToken,
                    cancellationToken);
                var operationTag = string.Concat("Publication-Id: ", publication.Id.Value.ToString("D"));
                string commitSha;
                if (sourceBranch is not null && sourceBranch.CommitMessage.Contains(operationTag, StringComparison.Ordinal))
                {
                    commitSha = sourceBranch.CommitSha;
                }
                else
                {
                    if (sourceBranch is not null
                        && !string.Equals(sourceBranch.CommitSha, target.BaseCommitSha, StringComparison.OrdinalIgnoreCase))
                    {
                        _ = publication.MarkTargetFailed(target.Id, "SOURCE_BRANCH_CONFLICT", clock.GetUtcNow());
                        continue;
                    }

                    await provider.CreateBranchAsync(
                        connection,
                        target.ProviderRepositoryId,
                        target.SourceBranch,
                        target.BaseCommitSha,
                        accessToken,
                        cancellationToken);
                    if (!await HasPublishPermissionAsync(author, publication, cancellationToken))
                    {
                        _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.PermissionRevoked, clock.GetUtcNow());
                        blocked = true;
                        FailUnstartedTargets(publication, PublicationValidationCodes.PermissionRevoked);
                        break;
                    }

                    var changes = await BuildCommitChangesAsync(target, cancellationToken);
                    var commitMessage = string.Concat(
                        publication.Message,
                        "\n\nPublished-by: ",
                        authorName,
                        "\n",
                        operationTag);
                    commitSha = await provider.CreateCommitAsync(
                        connection,
                        target.ProviderRepositoryId,
                        target.SourceBranch,
                        target.BaseCommitSha,
                        changes,
                        commitMessage,
                        accessToken,
                        cancellationToken);
                    var storedCommit = publication.RecordCommit(target.Id, commitSha);
                    if (storedCommit.IsError)
                    {
                        throw new InvalidOperationException("The publication commit could not be recorded.");
                    }
                }

                if (!await HasPublishPermissionAsync(author, publication, cancellationToken))
                {
                    _ = publication.RecordCommit(target.Id, commitSha);
                    _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.PermissionRevoked, clock.GetUtcNow());
                    blocked = true;
                    FailUnstartedTargets(publication, PublicationValidationCodes.PermissionRevoked);
                    break;
                }

                var existingPullRequest = await provider.GetOpenPullRequestAsync(
                    connection,
                    target.ProviderRepositoryId,
                    target.SourceBranch,
                    target.TargetBranch,
                    accessToken,
                    cancellationToken);
                var pullRequest = await provider.CreateOrUpdatePullRequestAsync(
                    connection,
                    target.ProviderRepositoryId,
                    existingPullRequest?.Id,
                    target.SourceBranch,
                    target.TargetBranch,
                    $"IFS révision {publication.RevisionNumber} — {project.Name}",
                    BuildPullRequestDescription(publication, revision, project, target),
                    project.DefaultReviewers,
                    accessToken,
                    cancellationToken);
                var recorded = publication.RecordPullRequest(
                    target.Id,
                    commitSha,
                    pullRequest.Id,
                    pullRequest.Url,
                    clock.GetUtcNow());
                if (recorded.IsError)
                {
                    throw new InvalidOperationException("The pull request could not be recorded for this publication.");
                }

                if (revision.State == RevisionState.Generated)
                {
                    revision.MarkPublished(clock.GetUtcNow());
                }

                project.RecordPublication(clock.GetUtcNow());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException exception) when (IsTransientProviderFailure(exception))
            {
                throw;
            }
            catch (Exception exception)
            {
                PublicationWriteFailed(logger, publication.Id.Value, target.Id, exception.GetType().Name);
                _ = publication.MarkTargetFailed(target.Id, PublicationValidationCodes.ProviderFailure, clock.GetUtcNow());
            }
        }
    }

    private async Task<PreparedTargetFiles> PrepareTargetFilesAsync(
        Publication publication,
        ProjectAggregate project,
        PublicationTarget target,
        string defaultBranch,
        IGitProvider provider,
        GitConnection connection,
        string? accessToken,
        IReadOnlyCollection<string> treePaths,
        PublicationTarget? priorTarget,
        CancellationToken cancellationToken)
    {
        var existingPaths = treePaths.ToHashSet(StringComparer.Ordinal);
        var preparedFiles = new List<PublicationTargetFileInput>();
        var requiresConfirmation = false;
        var plannedFiles = target.Files.ToDictionary(file => file.RepositoryPath, StringComparer.Ordinal);
        foreach (var destination in target.Destinations)
        {
            var manifestPath = JoinPath(destination.BasePath, ManifestPath);
            var manifestContent = await provider.GetFileContentAsync(
                connection,
                target.ProviderRepositoryId,
                defaultBranch,
                manifestPath,
                accessToken,
                cancellationToken);
            PublicationManifestDocument? manifest = null;
            Dictionary<string, PublicationManifestFileDocument> priorFiles = new(StringComparer.Ordinal);
            string? remoteManifestSha = null;
            if (manifestContent is null)
            {
                requiresConfirmation = true;
            }
            else
            {
                remoteManifestSha = HashSha256(manifestContent);
                try
                {
                    manifest = JsonSerializer.Deserialize<PublicationManifestDocument>(manifestContent, ManifestJsonOptions);
                }
                catch (JsonException)
                {
                    requiresConfirmation = true;
                }

                var ownsManifest = manifest is not null
                    && string.Equals(manifest.Schema, ManifestSchema, StringComparison.Ordinal)
                    && string.Equals(manifest.Project, project.Code, StringComparison.Ordinal)
                    && Guid.TryParse(manifest.OrganizationId, out var manifestOrganizationId)
                    && manifestOrganizationId == publication.OrganizationId.Value;
                if (!ownsManifest)
                {
                    requiresConfirmation = true;
                }
                else
                {
                    var priorManifestSha = priorTarget?.Files.SingleOrDefault(file =>
                        file.DestinationId == destination.DestinationId
                        && string.Equals(file.RepositoryPath, manifestPath, StringComparison.Ordinal))?.Sha256;
                    if (priorManifestSha is not null
                        && !string.Equals(priorManifestSha, remoteManifestSha, StringComparison.Ordinal))
                    {
                        requiresConfirmation = true;
                    }

                    foreach (var file in manifest!.Files ?? [])
                    {
                        if (string.IsNullOrWhiteSpace(file.Path)
                            || file.Sha256 is null
                            || !Sha256Pattern().IsMatch(file.Sha256)
                            || !IsWithinDestination(destination.BasePath, file.Path))
                        {
                            requiresConfirmation = true;
                            continue;
                        }

                        priorFiles.TryAdd(file.Path, file);
                    }
                }
            }

            var currentGenerated = plannedFiles.Values
                .Where(file => file.DestinationId == destination.DestinationId
                    && !string.Equals(file.RepositoryPath, manifestPath, StringComparison.Ordinal))
                .ToArray();
            var currentGeneratedPaths = currentGenerated.Select(file => file.RepositoryPath).ToHashSet(StringComparer.Ordinal);
            var remoteContentByPath = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var priorFile in priorFiles.Values)
            {
                if (!existingPaths.Contains(priorFile.Path))
                {
                    requiresConfirmation = true;
                    continue;
                }

                var currentContent = await provider.GetFileContentAsync(
                    connection,
                    target.ProviderRepositoryId,
                    defaultBranch,
                    priorFile.Path,
                    accessToken,
                    cancellationToken);
                remoteContentByPath[priorFile.Path] = currentContent;
                if (currentContent is null
                    || !string.Equals(HashSha256(currentContent), priorFile.Sha256, StringComparison.Ordinal))
                {
                    requiresConfirmation = true;
                }
            }

            foreach (var planned in currentGenerated)
            {
                string? currentContent;
                string? existingSha = null;
                if (existingPaths.Contains(planned.RepositoryPath))
                {
                    if (!remoteContentByPath.TryGetValue(planned.RepositoryPath, out currentContent))
                    {
                        currentContent = await provider.GetFileContentAsync(
                            connection,
                            target.ProviderRepositoryId,
                            defaultBranch,
                            planned.RepositoryPath,
                            accessToken,
                            cancellationToken);
                        remoteContentByPath[planned.RepositoryPath] = currentContent;
                    }

                    existingSha = currentContent is null ? null : HashSha256(currentContent);
                    if (!priorFiles.ContainsKey(planned.RepositoryPath))
                    {
                        requiresConfirmation = true;
                    }
                }
                else
                {
                    currentContent = null;
                    if (priorFiles.ContainsKey(planned.RepositoryPath))
                    {
                        requiresConfirmation = true;
                    }
                }

                var sameContent = existingSha is not null
                    && string.Equals(existingSha, planned.Sha256, StringComparison.Ordinal);
                var action = existingSha is null
                    ? PublicationFileAction.Add
                    : sameContent
                        ? PublicationFileAction.Unchanged
                        : PublicationFileAction.Update;
                preparedFiles.Add(new PublicationTargetFileInput(
                    destination.DestinationId,
                    planned.RepositoryPath,
                    planned.Sha256,
                    action,
                    existingSha));
            }

            foreach (var priorFile in priorFiles.Values.Where(file => !currentGeneratedPaths.Contains(file.Path)))
            {
                if (!existingPaths.Contains(priorFile.Path))
                {
                    continue;
                }

                var existingContent = remoteContentByPath.GetValueOrDefault(priorFile.Path);
                if (existingContent is null)
                {
                    continue;
                }

                preparedFiles.Add(new PublicationTargetFileInput(
                    destination.DestinationId,
                    priorFile.Path,
                    null,
                    PublicationFileAction.Delete,
                    HashSha256(existingContent)));
            }

            var newManifestContent = CreateManifestContent(
                project.Code,
                publication.OrganizationId,
                publication.RevisionNumber,
                publication.RequestedAt,
                currentGenerated);
            var manifestSha = HashSha256(newManifestContent);
            var manifestAction = remoteManifestSha is null
                ? PublicationFileAction.Add
                : string.Equals(remoteManifestSha, manifestSha, StringComparison.Ordinal)
                    ? PublicationFileAction.Unchanged
                    : PublicationFileAction.Update;
            await blobStore.EnsureFileAsync(
                manifestSha,
                Encoding.UTF8.GetBytes(newManifestContent),
                cancellationToken);
            preparedFiles.Add(new PublicationTargetFileInput(
                destination.DestinationId,
                manifestPath,
                manifestSha,
                manifestAction,
                remoteManifestSha));
        }

        return new PreparedTargetFiles(preparedFiles, requiresConfirmation);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<string>>> GetSourceDirectoriesByDestinationAsync(
        ProjectAggregate project,
        CancellationToken cancellationToken)
    {
        var components = await dbContext.Query<ComponentAggregate>()
            .AsNoTracking()
            .Where(component => component.ProjectId == project.Id)
            .ToArrayAsync(cancellationToken);
        var componentIds = components.Select(component => component.Id).ToArray();
        var applications = await dbContext.Query<ApplicationAggregate>()
            .AsNoTracking()
            .Where(application => componentIds.Contains(application.ComponentId))
            .ToArrayAsync(cancellationToken);
        return applications
            .Where(application => project.PublicationPlan.ResolveDestination(application.ComponentId.Value, PublishPart.SourceCode) is not null)
            .GroupBy(application => project.PublicationPlan.ResolveDestination(application.ComponentId.Value, PublishPart.SourceCode)!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)group.Select(application => application.CodeSourcePath.Trim('/'))
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());
    }

    private static bool HasRequiredSourceDirectories(
        PublicationTarget target,
        IReadOnlyCollection<string> treePaths,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> requiredPathsByDestination)
    {
        foreach (var destination in target.Destinations)
        {
            if (!requiredPathsByDestination.TryGetValue(destination.DestinationId, out var requiredPaths))
            {
                continue;
            }

            foreach (var requiredPath in requiredPaths)
            {
                if (!treePaths.Any(path => string.Equals(path, requiredPath, StringComparison.Ordinal)
                    || path.StartsWith(string.Concat(requiredPath, "/"), StringComparison.Ordinal)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private async Task<PublicationTarget?> FindPriorTargetAsync(
        ProjectId projectId,
        PublicationTarget target,
        CancellationToken cancellationToken)
    {
        var priorPublications = await dbContext.Query<Publication>()
            .Where(candidate => candidate.ProjectId == projectId
                && candidate.CompletedAt != null
                && (candidate.State == PublicationState.Completed || candidate.State == PublicationState.PartiallyPublished))
            .OrderByDescending(candidate => candidate.CompletedAt)
            .Take(50)
            .Include(candidate => candidate.Targets).ThenInclude(prior => prior.Files)
            .ToArrayAsync(cancellationToken);
        return priorPublications.SelectMany(candidate => candidate.Targets)
            .FirstOrDefault(prior => prior.GitConnectionId == target.GitConnectionId
                && string.Equals(prior.ProviderRepositoryId, target.ProviderRepositoryId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<Publication?> LoadPublicationAsync(
        Guid publicationId,
        OrganizationId organizationId,
        CancellationToken cancellationToken) =>
        await dbContext.Query<Publication>()
            .Include(candidate => candidate.Targets).ThenInclude(target => target.Destinations)
            .Include(candidate => candidate.Targets).ThenInclude(target => target.Files)
            .SingleOrDefaultAsync(candidate => candidate.Id == new PublicationId(publicationId)
                && candidate.OrganizationId == organizationId,
                cancellationToken);

    private async Task<GitConnection?> LoadConnectionAsync(GitConnectionId connectionId, CancellationToken cancellationToken) =>
        await dbContext.Query<GitConnection>()
            .Include(connection => connection.Repositories)
            .ThenInclude(repository => repository.AllowedProjects)
            .SingleOrDefaultAsync(connection => connection.Id == connectionId, cancellationToken);

    private async Task<string?> GetAccessTokenAsync(GitConnection connection, CancellationToken cancellationToken)
    {
        if (!connection.UsesFallbackToken)
        {
            return null;
        }

        return connection.SecretName is null
            ? null
            : await secretStore.GetAsync(connection.SecretName, cancellationToken);
    }

    private static bool IsRepositoryAccessible(GitConnection connection, PublicationTarget target, ProjectId projectId) =>
        connection.Repositories.Any(repository => repository.CanAccess(projectId)
            && string.Equals(repository.ProviderRepositoryId, target.ProviderRepositoryId, StringComparison.OrdinalIgnoreCase));

    private async Task<PublicationTarget?> FindChangedBaseAsync(
        Publication publication,
        CancellationToken cancellationToken)
    {
        foreach (var target in publication.Targets.Where(candidate => candidate.State == PublicationTargetState.Publishing))
        {
            var connection = await LoadConnectionAsync(target.GitConnectionId, cancellationToken);
            if (connection is null || !IsRepositoryAccessible(connection, target, publication.ProjectId))
            {
                continue;
            }

            var provider = providers.Get(connection.ProviderKind);
            var accessToken = await GetAccessTokenAsync(connection, cancellationToken);
            var branch = await provider.GetBranchAsync(
                connection,
                target.ProviderRepositoryId,
                target.TargetBranch,
                accessToken,
                cancellationToken);
            if (branch is null || !string.Equals(branch.CommitSha, target.BaseCommitSha, StringComparison.OrdinalIgnoreCase))
            {
                return target;
            }
        }

        return null;
    }

    private async Task<bool> HasPublishPermissionAsync(
        UserKey author,
        Publication publication,
        CancellationToken cancellationToken)
    {
        var authorization = await projectAuthorizer.GetProjectAuthorizationContextAsync(
            author,
            publication.OrganizationId,
            publication.ProjectId,
            cancellationToken);
        return authorization is not null && authorization.Permissions.Contains(Permission.Publish);
    }

    private async Task<string> GetAuthorNameAsync(UserKey author, CancellationToken cancellationToken)
    {
        var displayName = await dbContext.Query<UserProfile>()
            .Where(profile => profile.TenantId == author.TenantId && profile.ObjectId == author.ObjectId)
            .Select(profile => profile.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(displayName) ? author.ObjectId.ToString("D") : displayName;
    }

    private void FailUnstartedTargets(Publication publication, string failureCode)
    {
        foreach (var target in publication.Targets.Where(target => target.State == PublicationTargetState.Publishing))
        {
            _ = publication.MarkTargetFailed(target.Id, failureCode, clock.GetUtcNow());
        }
    }

    private async Task<IReadOnlyCollection<GitCommitChange>> BuildCommitChangesAsync(
        PublicationTarget target,
        CancellationToken cancellationToken)
    {
        var changes = new List<GitCommitChange>();
        foreach (var file in target.Files.Where(file => file.Action != PublicationFileAction.Unchanged))
        {
            if (file.Action == PublicationFileAction.Delete)
            {
                changes.Add(new GitCommitChange(file.RepositoryPath, null, GitCommitChangeKind.Delete));
                continue;
            }

            if (file.Sha256 is null)
            {
                throw new InvalidDataException("A generated publication file is missing its content hash.");
            }

            await using var stream = await blobStore.OpenFileAsync(file.Sha256, cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: false);
            var content = await reader.ReadToEndAsync(cancellationToken);
            changes.Add(new GitCommitChange(
                file.RepositoryPath,
                content,
                file.Action == PublicationFileAction.Add ? GitCommitChangeKind.Add : GitCommitChangeKind.Edit));
        }

        return changes;
    }

    private static string BuildPullRequestDescription(
        Publication publication,
        RevisionAggregate revision,
        ProjectAggregate project,
        PublicationTarget target)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Révision **{publication.RevisionNumber}** du projet **{project.Name}** (`{project.Code}`).");
        builder.AppendLine();
        builder.AppendLine("Fichiers de ce dépôt :");
        foreach (var file in target.Files.Where(file => file.Action != PublicationFileAction.Unchanged)
                     .Take(MaximumDescriptionFiles))
        {
            builder.Append("- ").Append(file.Action switch
            {
                PublicationFileAction.Add => "Ajout",
                PublicationFileAction.Update => "Modification",
                PublicationFileAction.Delete => "Suppression",
                _ => "Conservation"
            }).Append(": `").Append(file.RepositoryPath).AppendLine("`");
        }

        if (target.Files.Count(file => file.Action != PublicationFileAction.Unchanged) > MaximumDescriptionFiles)
        {
            builder.AppendLine("- …");
        }

        if (revision.Findings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Constats de la révision :");
            foreach (var finding in revision.Findings.Take(10))
            {
                builder.Append("- ").Append(finding.Code).Append(": ").AppendLine(finding.MessageFr);
            }
        }

        return builder.ToString();
    }

    private static bool IsTransientProviderFailure(HttpRequestException exception)
    {
        var status = exception.StatusCode;
        return status is null
            || status == HttpStatusCode.RequestTimeout
            || status == HttpStatusCode.TooManyRequests
            || (int)status.GetValueOrDefault() >= 500;
    }

    private static string CreateManifestContent(
        string projectCode,
        OrganizationId organizationId,
        int revisionNumber,
        DateTimeOffset generatedAtUtc,
        IReadOnlyCollection<PublicationTargetFile> files)
    {
        var manifest = new PublicationManifestDocument(
            ManifestSchema,
            projectCode,
            organizationId.Value.ToString("D"),
            revisionNumber,
            generatedAtUtc,
            files.OrderBy(file => file.RepositoryPath, StringComparer.Ordinal)
                .Select(file => new PublicationManifestFileDocument(file.RepositoryPath, file.Sha256!))
                .ToArray());
        return JsonSerializer.Serialize(manifest, ManifestJsonOptions);
    }

    private static bool IsWithinDestination(string basePath, string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0 || normalized.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            return false;
        }

        return basePath.Length == 0
            || string.Equals(normalized, basePath, StringComparison.Ordinal)
            || normalized.StartsWith(string.Concat(basePath, "/"), StringComparison.Ordinal);
    }

    private static string JoinPath(string basePath, string path) =>
        basePath.Length == 0 ? path : string.Concat(basePath, "/", path);

    private static string HashSha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    [System.Text.RegularExpressions.GeneratedRegex("\\A[0-9a-f]{64}\\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex Sha256Pattern();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publication {PublicationId} target {TargetId} preparation failed with {FailureType}.")]
    private static partial void PublicationTargetFailed(ILogger logger, Guid publicationId, Guid targetId, string failureType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publication {PublicationId} target {TargetId} write failed with {FailureType}.")]
    private static partial void PublicationWriteFailed(ILogger logger, Guid publicationId, Guid targetId, string failureType);

    private sealed record PreparedTargetFiles(
        IReadOnlyCollection<PublicationTargetFileInput> Files,
        bool RequiresConfirmation);

    private sealed record PublicationManifestDocument(
        [property: JsonPropertyName("schema")] string? Schema,
        [property: JsonPropertyName("project")] string? Project,
        [property: JsonPropertyName("organizationId")] string? OrganizationId,
        [property: JsonPropertyName("revision")] int Revision,
        [property: JsonPropertyName("generatedAtUtc")] DateTimeOffset GeneratedAtUtc,
        [property: JsonPropertyName("files")] IReadOnlyCollection<PublicationManifestFileDocument>? Files);

    private sealed record PublicationManifestFileDocument(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("sha256")] string? Sha256);
}
