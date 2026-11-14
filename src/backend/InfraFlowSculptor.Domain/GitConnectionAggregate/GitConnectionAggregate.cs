using ErrorOr;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.GitConnectionAggregate;

public sealed class GitConnectionAggregate : AggregateRoot<GitConnectionId>, IOrganizationOwned, IHasVersion
{
    private readonly List<GitConnectionRepository> repositories = [];

    private GitConnectionAggregate()
    {
    }

    private GitConnectionAggregate(
        GitConnectionId id,
        OrganizationId organizationId,
        GitProviderKind providerKind,
        string name,
        string organization,
        Guid? tenantId,
        GitConnectionAuthenticationMode authenticationMode,
        DateTimeOffset? personalAccessTokenExpiresAt,
        string? secretName)
        : base(id)
    {
        OrganizationId = organizationId;
        ProviderKind = providerKind;
        Name = name;
        Organization = organization;
        TenantId = tenantId;
        AuthenticationMode = authenticationMode;
        PersonalAccessTokenExpiresAt = personalAccessTokenExpiresAt;
        SecretName = secretName;
    }

    public OrganizationId OrganizationId { get; private set; }

    public GitProviderKind ProviderKind { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Organization { get; private set; } = string.Empty;

    public Guid? TenantId { get; private set; }

    public GitConnectionAuthenticationMode AuthenticationMode { get; private set; }

    public DateTimeOffset? PersonalAccessTokenExpiresAt { get; private set; }

    public string? SecretName { get; private set; }

    public IReadOnlyCollection<GitConnectionRepository> Repositories => repositories.AsReadOnly();

    public int Version { get; private set; } = 1;

    public bool Expiry15DayAlertSent { get; private set; }

    public bool Expiry3DayAlertSent { get; private set; }

    public bool UsesFallbackToken => AuthenticationMode == GitConnectionAuthenticationMode.PersonalAccessToken;

    public ErrorOr<Success> UpdateDetails(
        string name,
        string organization,
        Guid? tenantId,
        DateTimeOffset? personalAccessTokenExpiresAt,
        DateTimeOffset now)
    {
        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 80)
        {
            return Error.Validation("VAL-PUB-CONNEXION", "A connection name of 1 to 80 characters is required.");
        }

        var normalizedOrganization = organization?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedOrganization) || normalizedOrganization.Length > 300)
        {
            return Error.Validation("VAL-PUB-CONNEXION", "A provider organization or endpoint is required.");
        }

        if (UsesFallbackToken && (personalAccessTokenExpiresAt is null || personalAccessTokenExpiresAt <= now))
        {
            return Error.Validation("VAL-PUB-CONNEXION", "A fallback token requires a future expiration date.");
        }

        if (!UsesFallbackToken && personalAccessTokenExpiresAt is not null)
        {
            return Error.Validation(
                "VAL-PUB-CONNEXION",
                "Only fallback-token connections may declare a token expiration date.");
        }

        if (AuthenticationMode == GitConnectionAuthenticationMode.FederatedServicePrincipal
            && (tenantId is null || tenantId == Guid.Empty))
        {
            return Error.Validation(
                "VAL-PUB-CONNEXION",
                "Federated authentication requires the Azure DevOps organization's Entra tenant identifier.");
        }

        if (PersonalAccessTokenExpiresAt != personalAccessTokenExpiresAt)
        {
            Expiry15DayAlertSent = false;
            Expiry3DayAlertSent = false;
        }

        Name = normalizedName;
        Organization = normalizedOrganization;
        TenantId = tenantId;
        PersonalAccessTokenExpiresAt = personalAccessTokenExpiresAt;
        Version = checked(Version + 1);
        return Result.Success;
    }

    public ErrorOr<Success> MarkExpiryAlertSent(int daysBeforeExpiry)
    {
        if (!UsesFallbackToken || daysBeforeExpiry is not (15 or 3))
        {
            return Error.Validation("VAL-PUB-CONNEXION", "Only 15-day and 3-day fallback-token alerts are supported.");
        }

        if (daysBeforeExpiry == 15)
        {
            Expiry15DayAlertSent = true;
        }
        else
        {
            Expiry3DayAlertSent = true;
        }

        Version = checked(Version + 1);
        return Result.Success;
    }

    public static ErrorOr<GitConnectionAggregate> Create(
        GitConnectionId id,
        OrganizationId organizationId,
        GitProviderKind providerKind,
        string name,
        string organization,
        Guid? tenantId,
        GitConnectionAuthenticationMode authenticationMode,
        DateTimeOffset? personalAccessTokenExpiresAt,
        string? secretName,
        DateTimeOffset now)
    {
        if (id.Value == Guid.Empty || organizationId.Value == Guid.Empty)
        {
            return Error.Validation("VAL-PUB-CONNEXION", "A connection and organization identifier are required.");
        }

        if (!Enum.IsDefined(providerKind) || !Enum.IsDefined(authenticationMode))
        {
            return Error.Validation("VAL-PUB-CONNEXION", "The selected provider or authentication mode is invalid.");
        }

        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 80)
        {
            return Error.Validation("VAL-PUB-CONNEXION", "A connection name of 1 to 80 characters is required.");
        }

        var normalizedOrganization = organization?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedOrganization) || normalizedOrganization.Length > 300)
        {
            return Error.Validation("VAL-PUB-CONNEXION", "A provider organization or endpoint is required.");
        }

        if (authenticationMode == GitConnectionAuthenticationMode.FederatedServicePrincipal
            && (tenantId is null || tenantId == Guid.Empty))
        {
            return Error.Validation(
                "VAL-PUB-CONNEXION",
                "Federated authentication requires the Azure DevOps organization's Entra tenant identifier.");
        }

        if (authenticationMode == GitConnectionAuthenticationMode.PersonalAccessToken
            && (personalAccessTokenExpiresAt is null
                || personalAccessTokenExpiresAt <= now
                || string.IsNullOrWhiteSpace(secretName)))
        {
            return Error.Validation(
                "VAL-PUB-CONNEXION",
                "A fallback token requires a future expiration date and a secret-store reference.");
        }

        if (authenticationMode != GitConnectionAuthenticationMode.PersonalAccessToken
            && personalAccessTokenExpiresAt is not null)
        {
            return Error.Validation(
                "VAL-PUB-CONNEXION",
                "Only fallback-token connections may declare a token expiration date.");
        }

        return new GitConnectionAggregate(
            id,
            organizationId,
            providerKind,
            normalizedName,
            normalizedOrganization,
            tenantId,
            authenticationMode,
            personalAccessTokenExpiresAt,
            secretName?.Trim());
    }

    public ErrorOr<Success> ReplaceRepositories(IEnumerable<GitConnectionRepository> availableRepositories)
    {
        ArgumentNullException.ThrowIfNull(availableRepositories);
        var incoming = availableRepositories.ToArray();
        if (incoming.Any(repository => string.IsNullOrWhiteSpace(repository.ProviderRepositoryId)
                                       || string.IsNullOrWhiteSpace(repository.Name)
                                       || string.IsNullOrWhiteSpace(repository.DefaultBranch))
            || incoming.Select(repository => repository.ProviderRepositoryId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != incoming.Length)
        {
            return Error.Validation("VAL-PUB-CONNEXION", "The provider returned invalid or duplicate repositories.");
        }

        var existing = repositories.ToDictionary(
            repository => repository.ProviderRepositoryId,
            StringComparer.OrdinalIgnoreCase);
        repositories.Clear();
        foreach (var repository in incoming)
        {
            if (existing.TryGetValue(repository.ProviderRepositoryId, out var prior))
            {
                repository.OpenTo(prior.AllowedProjects.Select(access => access.ProjectId));
                if (prior.OpenToAllProjects)
                {
                    repository.OpenToAll();
                }
            }

            repositories.Add(repository);
        }

        Version = checked(Version + 1);
        return Result.Success;
    }

    public ErrorOr<Success> OpenRepositoryToAllProjects(string providerRepositoryId)
    {
        var repository = FindRepository(providerRepositoryId);
        if (repository.IsError)
        {
            return repository.Errors;
        }

        repository.Value.OpenToAll();
        Version = checked(Version + 1);
        return Result.Success;
    }

    public ErrorOr<Success> OpenRepositoryToProjects(
        string providerRepositoryId,
        IEnumerable<ProjectId> projectIds)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        var ids = projectIds.Distinct().ToArray();
        if (ids.Length == 0 || ids.Any(projectId => projectId.Value == Guid.Empty))
        {
            return Error.Validation("VAL-PUB-CONNEXION", "At least one valid project must be selected.");
        }

        var repository = FindRepository(providerRepositoryId);
        if (repository.IsError)
        {
            return repository.Errors;
        }

        repository.Value.OpenTo(ids);
        Version = checked(Version + 1);
        return Result.Success;
    }

    public ErrorOr<Success> CloseRepository(string providerRepositoryId)
    {
        var repository = FindRepository(providerRepositoryId);
        if (repository.IsError)
        {
            return repository.Errors;
        }

        repository.Value.Close();
        Version = checked(Version + 1);
        return Result.Success;
    }

    public bool CanAccessRepository(string providerRepositoryId, ProjectId projectId) =>
        repositories.SingleOrDefault(repository =>
            string.Equals(repository.ProviderRepositoryId, providerRepositoryId, StringComparison.OrdinalIgnoreCase))
            ?.CanAccess(projectId) == true;

    private ErrorOr<GitConnectionRepository> FindRepository(string providerRepositoryId)
    {
        if (string.IsNullOrWhiteSpace(providerRepositoryId))
        {
            return Error.Validation("VAL-PUB-CONNEXION", "A provider repository identifier is required.");
        }

        var repository = repositories.SingleOrDefault(candidate =>
            string.Equals(candidate.ProviderRepositoryId, providerRepositoryId, StringComparison.OrdinalIgnoreCase));
        return repository is null
            ? Error.NotFound("NOT_FOUND", "The repository was not found.")
            : repository;
    }
}
