using ErrorOr;
using InfraFlowSculptor.Domain.GitConnectionAggregate;

namespace InfraFlowSculptor.Domain.ProjectAggregate;

public sealed class PublishPlanDestination
{
    private PublishPlanDestination()
    {
    }

    private PublishPlanDestination(Guid id, GitConnectionId gitConnectionId, string providerRepositoryId, string basePath)
    {
        Id = id;
        GitConnectionId = gitConnectionId;
        ProviderRepositoryId = providerRepositoryId;
        BasePath = basePath;
    }

    public Guid Id { get; private set; }

    public GitConnectionId GitConnectionId { get; private set; }

    public string ProviderRepositoryId { get; private set; } = string.Empty;

    public string BasePath { get; private set; } = string.Empty;

    public static ErrorOr<PublishPlanDestination> Create(PublishPlanDestinationInput input)
    {
        if (input is null || input.Id == Guid.Empty || input.GitConnectionId == Guid.Empty)
        {
            return Error.Validation(PublishValidationCodes.Destination, "Une destination exige une connexion et un identifiant valides.");
        }

        var repositoryId = input.ProviderRepositoryId?.Trim();
        if (string.IsNullOrWhiteSpace(repositoryId) || repositoryId.Length > 300)
        {
            return Error.Validation(PublishValidationCodes.Destination, "Le dépôt de destination doit être renseigné.");
        }

        var normalizedPath = NormalizeBasePath(input.BasePath);
        if (normalizedPath.IsError)
        {
            return normalizedPath.Errors;
        }

        return new PublishPlanDestination(input.Id, new GitConnectionId(input.GitConnectionId), repositoryId, normalizedPath.Value);
    }

    private static ErrorOr<string> NormalizeBasePath(string? basePath)
    {
        var candidate = (basePath ?? string.Empty).Trim().Replace('\\', '/');
        if (candidate.Length == 0)
        {
            return string.Empty;
        }

        if (candidate.StartsWith('/')
            || candidate.StartsWith("//", StringComparison.Ordinal)
            || (candidate.Length >= 2 && char.IsAsciiLetter(candidate[0]) && candidate[1] == ':'))
        {
            return Error.Validation(PublishValidationCodes.Destination, "Le chemin de base doit être relatif au dépôt.");
        }

        var segments = candidate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            return Error.Validation(PublishValidationCodes.Destination, "Le chemin de base ne peut pas contenir de segment « .. ».");
        }

        var normalized = string.Join('/', segments);
        if (normalized.Length > 200)
        {
            return Error.Validation(PublishValidationCodes.Destination, "Le chemin de base ne peut pas dépasser 200 caractères.");
        }

        return normalized;
    }
}
