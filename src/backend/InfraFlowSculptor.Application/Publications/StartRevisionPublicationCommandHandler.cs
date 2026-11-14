using ErrorOr;
using InfraFlowSculptor.Application.Common.Jobs;
using InfraFlowSculptor.Application.Common.Observability;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.PublicationAggregate;
using InfraFlowSculptor.Domain.RevisionAggregate;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Engine.Publication;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Publication = InfraFlowSculptor.Domain.PublicationAggregate.PublicationAggregate;

namespace InfraFlowSculptor.Application.Publications;

public sealed class StartRevisionPublicationCommandHandler(
    IIfsDbContext dbContext,
    ICurrentOrganization currentOrganization,
    ICurrentUser currentUser,
    IJobDispatcher jobDispatcher,
    TimeProvider clock)
    : ICommandHandler<StartRevisionPublicationCommand, ErrorOr<PublicationView>>
{
    private static readonly PublicationState[] ActiveStates =
    [
        PublicationState.Preparing,
        PublicationState.NeedsConfirmation,
        PublicationState.Prepared,
        PublicationState.Publishing
    ];

    public async ValueTask<ErrorOr<PublicationView>> Handle(
        StartRevisionPublicationCommand command,
        CancellationToken cancellationToken)
    {
        var projectId = new ProjectId(command.ProjectId);
        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("PROJECT_NOT_FOUND", "Le projet n'a pas été trouvé.");
        }

        if (!await dbContext.LockProjectForUpdateAsync(projectId, cancellationToken))
        {
            return Error.NotFound("PROJECT_NOT_FOUND", "Le projet n'a pas été trouvé.");
        }

        dbContext.ClearTrackedChanges();
        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.PublicationPlan.Destinations)
            .Include(candidate => candidate.PublicationPlan.Assignments)
            .SingleOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return Error.NotFound("PROJECT_NOT_FOUND", "Le projet n'a pas été trouvé.");
        }

        var revision = await dbContext.Query<RevisionAggregate>()
            .Include(candidate => candidate.Files)
            .Include(candidate => candidate.Findings)
            .Include(candidate => candidate.Changes)
            .SingleOrDefaultAsync(candidate => candidate.Id == new RevisionId(command.RevisionId)
                && candidate.ProjectId == projectId,
                cancellationToken);
        if (revision is null)
        {
            return Error.NotFound("REVISION_NOT_FOUND", "La révision n'a pas été trouvée.");
        }

        if (revision.State != RevisionState.Generated)
        {
            return Error.Conflict(PublicationValidationCodes.RevisionNotGenerated, "Seule une révision générée peut être publiée.");
        }

        var isStale = revision.ModelVersion != project.ModelVersion
            || !string.Equals(revision.CatalogVersion, project.CatalogVersion, StringComparison.Ordinal)
            || !string.Equals(revision.InfrastructureLanguage, project.InfrastructureLanguage, StringComparison.Ordinal)
            || !string.Equals(revision.CiPlatform, project.CiPlatform, StringComparison.Ordinal);
        if (isStale && !command.AcceptStaleRevision)
        {
            return Error.Conflict(PublicationValidationCodes.StaleRevision, "La révision ne correspond plus au modèle courant. Confirmez sa publication ou générez une nouvelle révision.");
        }

        var priorForRevision = await dbContext.Query<Publication>()
            .Include(candidate => candidate.Targets).ThenInclude(target => target.Destinations)
            .Include(candidate => candidate.Targets).ThenInclude(target => target.Files)
            .Where(candidate => candidate.ProjectId == projectId && candidate.RevisionId == revision.Id)
            .OrderByDescending(candidate => candidate.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (priorForRevision is not null
            && priorForRevision.State is not (PublicationState.Failed or PublicationState.PartiallyPublished))
        {
            return PublicationViewMapper.Map(priorForRevision);
        }

        if (command.DestinationIds is { Count: 0 })
        {
            return Error.Validation(PublicationValidationCodes.InvalidTarget, "La reprise doit sélectionner au moins une destination.");
        }

        if (command.DestinationIds is { } selectedDestinationIds)
        {
            if (priorForRevision is null
                || !PublicationRetrySelection.IsValid(priorForRevision, selectedDestinationIds))
            {
                return Error.Validation(PublicationValidationCodes.InvalidTarget, "Les destinations sélectionnées ne correspondent pas à une publication en échec.");
            }
        }
        else if (priorForRevision?.State == PublicationState.PartiallyPublished)
        {
            return PublicationViewMapper.Map(priorForRevision);
        }

        var activePublicationExists = await dbContext.Query<Publication>()
            .AnyAsync(candidate => candidate.ProjectId == projectId && ActiveStates.Contains(candidate.State), cancellationToken);
        if (activePublicationExists)
        {
            return Error.Conflict(PublicationValidationCodes.ProjectBusy, "Une autre publication est déjà en cours pour ce projet.");
        }

        var targetInputs = await BuildTargetsAsync(project, revision, command.DestinationIds, cancellationToken);
        if (targetInputs.IsError)
        {
            return targetInputs.Errors;
        }

        var publicationId = PublicationId.New(clock);
        var publicationResult = Publication.Start(
            publicationId,
            organizationId,
            projectId,
            revision.Id,
            revision.Number,
            currentUser.Key,
            clock.GetUtcNow(),
            command.Message,
            targetInputs.Value);
        if (publicationResult.IsError)
        {
            return publicationResult.Errors;
        }

        dbContext.Query<Publication>().Add(publicationResult.Value);
        jobDispatcher.Enqueue(JobQueue.Publication, organizationId, new PreparePublicationJob(publicationId.Value));
        IfsTelemetry.RecordPublication(organizationId);
        return PublicationViewMapper.Map(publicationResult.Value);
    }

    private async Task<ErrorOr<IReadOnlyCollection<PublicationTargetInput>>> BuildTargetsAsync(
        ProjectAggregate project,
        RevisionAggregate revision,
        IReadOnlyCollection<Guid>? selectedDestinationIds,
        CancellationToken cancellationToken)
    {
        if (project.PublicationPlan.Destinations.Count == 0)
        {
            return Error.Validation(PublishValidationCodes.Destination, "Aucun dépôt n'est configuré dans le plan de publication.");
        }

        var components = await dbContext.Query<ComponentAggregate>()
            .AsNoTracking()
            .Where(component => component.ProjectId == project.Id)
            .OrderBy(component => component.NameKey)
            .ToArrayAsync(cancellationToken);
        var componentIds = components.Select(component => component.Id).ToArray();
        var applications = await dbContext.Query<ApplicationAggregate>()
            .AsNoTracking()
            .Where(application => componentIds.Contains(application.ComponentId))
            .ToArrayAsync(cancellationToken);
        var applicationsByComponent = applications.GroupBy(application => application.ComponentId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var componentViews = components.Select(component => new PublishComponent(
            component.Id.Value,
            component.Code,
            applicationsByComponent.ContainsKey(component.Id))).ToArray();
        var sourceCodePaths = applicationsByComponent.ToDictionary(
            group => group.Key.Value,
            group => group.Value.Select(application => application.CodeSourcePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .FirstOrDefault() ?? string.Empty);

        var connections = await dbContext.Query<GitConnectionAggregate>()
            .AsNoTracking()
            .Include(connection => connection.Repositories)
            .ThenInclude(repository => repository.AllowedProjects)
            .ToArrayAsync(cancellationToken);
        var connectionsById = connections.ToDictionary(connection => connection.Id);
        var providerByDestinationId = new Dictionary<Guid, GitProviderKind>();
        foreach (var destination in project.PublicationPlan.Destinations)
        {
            if (!connectionsById.TryGetValue(destination.GitConnectionId, out var connection))
            {
                return Error.Validation(PublishValidationCodes.Destination, "Une connexion git du plan n'existe plus.");
            }

            var repository = connection.Repositories.SingleOrDefault(candidate =>
                string.Equals(candidate.ProviderRepositoryId, destination.ProviderRepositoryId, StringComparison.OrdinalIgnoreCase));
            if (repository is null || !repository.CanAccess(project.Id))
            {
                return Error.Validation(PublishValidationCodes.Destination, "Un dépôt configuré n'existe plus ou n'est plus accessible à ce projet.");
            }

            providerByDestinationId.Add(destination.Id, connection.ProviderKind);
        }

        var layout = PublishLayout.Build(
            project.PublicationPlan,
            project.CiPlatform,
            componentViews,
            revision.Files.Select(file => file.RelativePath).ToArray(),
            providerByDestinationId);
        var blockingIssue = layout.Issues.FirstOrDefault(issue => issue.Code != PublishValidationCodes.CiCompatibility);
        if (blockingIssue is not null)
        {
            return Error.Validation(PublishValidationCodes.Destination, blockingIssue.Message);
        }

        var revisionFilesByPath = revision.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
        var selectedIds = selectedDestinationIds?.ToHashSet();
        if (selectedIds is not null
            && selectedIds.Any(destinationId => project.PublicationPlan.Destinations.All(destination => destination.Id != destinationId)))
        {
            return Error.Validation(PublicationValidationCodes.InvalidTarget, "Une destination sélectionnée n'appartient pas au plan de publication courant.");
        }

        var filesByDestination = layout.Files.GroupBy(file => file.DestinationId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var targets = new List<PublicationTargetInput>();
        foreach (var destinationGroup in project.PublicationPlan.Destinations
                     .Where(destination => selectedIds is null || selectedIds.Contains(destination.Id))
                     .GroupBy(destination => (destination.GitConnectionId, destination.ProviderRepositoryId), new RepositoryKeyComparer()))
        {
            var connection = connectionsById[destinationGroup.Key.GitConnectionId];
            var repository = connection.Repositories.Single(candidate =>
                string.Equals(candidate.ProviderRepositoryId, destinationGroup.Key.ProviderRepositoryId, StringComparison.OrdinalIgnoreCase));
            var branchName = $"ifs/{project.Code}/revision-{revision.Number}";
            var destinationInputs = destinationGroup.Select(destination =>
                new PublicationTargetDestinationInput(destination.Id, destination.BasePath)).ToArray();
            var targetFiles = destinationGroup.SelectMany(destination =>
                    filesByDestination.GetValueOrDefault(destination.Id, []))
                .Select(file => new PublicationTargetFileInput(
                    file.DestinationId,
                    file.DestinationPath,
                    revisionFilesByPath[file.SourcePath].Sha256,
                    PublicationFileAction.Add,
                    null))
                .ToArray();
            targets.Add(new PublicationTargetInput(
                destinationGroup.Key.GitConnectionId,
                repository.ProviderRepositoryId,
                repository.Name,
                NormalizeBranch(repository.DefaultBranch),
                branchName,
                destinationInputs,
                targetFiles));
        }

        if (selectedIds is not null
            && targets.SelectMany(target => target.Destinations).Select(destination => destination.DestinationId).Distinct().Count() != selectedIds.Count)
        {
            return Error.Validation(PublicationValidationCodes.InvalidTarget, "Une destination sélectionnée ne peut pas être préparée.");
        }

        return targets;
    }

    private static string NormalizeBranch(string branchName) => branchName.StartsWith("refs/heads/", StringComparison.Ordinal)
        ? branchName["refs/heads/".Length..]
        : branchName;

    private sealed class RepositoryKeyComparer : IEqualityComparer<(GitConnectionId GitConnectionId, string ProviderRepositoryId)>
    {
        public bool Equals(
            (GitConnectionId GitConnectionId, string ProviderRepositoryId) x,
            (GitConnectionId GitConnectionId, string ProviderRepositoryId) y) =>
            x.GitConnectionId == y.GitConnectionId
            && string.Equals(x.ProviderRepositoryId, y.ProviderRepositoryId, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((GitConnectionId GitConnectionId, string ProviderRepositoryId) value) =>
            HashCode.Combine(value.GitConnectionId, StringComparer.OrdinalIgnoreCase.GetHashCode(value.ProviderRepositoryId));
    }
}
