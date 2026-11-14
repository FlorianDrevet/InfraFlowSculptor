using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Engine.Publication;

public static class PublishLayout
{
    public static string EffectiveFolder(
        string componentCode,
        PublishPart part,
        string basePath,
        string? sourceCodePath = null)
    {
        if (part == PublishPart.SourceCode)
        {
            var sourceFolder = (sourceCodePath ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
            return sourceFolder.Length == 0 ? string.Empty : string.Concat(sourceFolder, "/");
        }

        var componentFolder = part switch
        {
            PublishPart.Infrastructure => "infra",
            PublishPart.Applications => "apps",
            _ => string.Empty
        };
        var baseFolder = (basePath ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        var relativeFolder = string.Join('/', new[] { componentCode, componentFolder }.Where(segment => segment.Length > 0));
        return baseFolder.Length == 0 ? relativeFolder : string.Concat(baseFolder, "/", relativeFolder);
    }

    public static PublishLayoutResult Build(
        PublishPlan plan,
        string ciPlatform,
        IReadOnlyCollection<PublishComponent> components,
        IReadOnlyCollection<string> logicalPaths,
        IReadOnlyDictionary<Guid, GitProviderKind> providerByDestinationId,
        PublishCiCompatibilityRegistry? compatibilityRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(logicalPaths);
        ArgumentNullException.ThrowIfNull(providerByDestinationId);
        compatibilityRegistry ??= PublishCiCompatibilityRegistry.Pilot;

        var issues = new List<PublishLayoutIssue>();
        var destinationsById = plan.Destinations.ToDictionary(destination => destination.Id);
        var componentsByCode = new Dictionary<string, PublishComponent>(StringComparer.Ordinal);
        foreach (var component in components)
        {
            if (string.IsNullOrWhiteSpace(component.Code)
                || !componentsByCode.TryAdd(component.Code, component))
            {
                issues.Add(new PublishLayoutIssue(
                    PublishValidationCodes.Destination,
                    "Chaque composant doit avoir un code distinct pour répartir les fichiers."));
            }
        }

        var componentsById = components.ToDictionary(component => component.Id);
        foreach (var assignment in plan.Assignments)
        {
            if (!componentsById.TryGetValue(assignment.ComponentId, out var component)
                || (assignment.Part != PublishPart.Infrastructure
                    && (!component.HasApplications || assignment.Part is not (PublishPart.Applications or PublishPart.SourceCode))))
            {
                issues.Add(new PublishLayoutIssue(
                    PublishValidationCodes.Destination,
                    "Une affectation de publication pointe vers une partie de composant absente.",
                    assignment.ComponentId,
                    assignment.Part,
                    assignment.DestinationId));
            }
        }

        foreach (var destination in plan.Destinations)
        {
            if (!providerByDestinationId.TryGetValue(destination.Id, out var providerKind))
            {
                issues.Add(new PublishLayoutIssue(
                    PublishValidationCodes.Destination,
                    "Le fournisseur de la destination est introuvable.",
                    DestinationId: destination.Id));
            }
            else if (!compatibilityRegistry.IsCompatible(ciPlatform, providerKind))
            {
                issues.Add(new PublishLayoutIssue(
                    PublishValidationCodes.CiCompatibility,
                    "Le dépôt n'est pas compatible avec la plateforme CI du projet.",
                    DestinationId: destination.Id));
            }
        }

        var generatedDestinationIds = new HashSet<Guid>();
        foreach (var component in components)
        {
            ValidateAssignment(plan, destinationsById, component, PublishPart.Infrastructure, generatedDestinationIds, issues);
            if (component.HasApplications)
            {
                ValidateAssignment(plan, destinationsById, component, PublishPart.Applications, generatedDestinationIds, issues);
                ValidateAssignment(plan, destinationsById, component, PublishPart.SourceCode, null, issues);
            }
        }

        var files = new List<PublishLayoutFile>();
        var usedPaths = new HashSet<(Guid DestinationId, string DestinationPath)>();
        foreach (var rawPath in logicalPaths)
        {
            var path = NormalizeLogicalPath(rawPath);
            if (path is null)
            {
                issues.Add(new PublishLayoutIssue(
                    PublishValidationCodes.Destination,
                    "Le chemin logique généré doit être relatif et ne contenir aucun segment « .. »."));
                continue;
            }

            var segments = path.Split('/');
            if (componentsByCode.TryGetValue(segments[0], out var component))
            {
                if (segments.Length < 3 || !TryGetGeneratedPart(segments[1], out var part))
                {
                    issues.Add(new PublishLayoutIssue(
                        PublishValidationCodes.Destination,
                        "Un fichier de composant doit appartenir à son dossier infra ou apps.",
                        component.Id));
                    continue;
                }

                var destinationId = plan.ResolveDestination(component.Id, part);
                if (destinationId is null || !destinationsById.TryGetValue(destinationId.Value, out var destination))
                {
                    issues.Add(new PublishLayoutIssue(
                        PublishValidationCodes.Destination,
                        "Aucune destination n'est définie pour cette partie du composant.",
                        component.Id,
                        part,
                        destinationId));
                    continue;
                }

                AddFile(destination, path, files, usedPaths, issues, component.Id, part);
                continue;
            }

            foreach (var destinationId in generatedDestinationIds.Order())
            {
                if (destinationsById.TryGetValue(destinationId, out var destination))
                {
                    AddFile(destination, path, files, usedPaths, issues);
                }
            }
        }

        return new PublishLayoutResult(
            files.OrderBy(file => file.DestinationId).ThenBy(file => file.DestinationPath, StringComparer.Ordinal).ToArray(),
            issues);
    }

    private static void ValidateAssignment(
        PublishPlan plan,
        Dictionary<Guid, PublishPlanDestination> destinationsById,
        PublishComponent component,
        PublishPart part,
        HashSet<Guid>? generatedDestinationIds,
        List<PublishLayoutIssue> issues)
    {
        var destinationId = plan.ResolveDestination(component.Id, part);
        if (destinationId is null || !destinationsById.ContainsKey(destinationId.Value))
        {
            issues.Add(new PublishLayoutIssue(
                PublishValidationCodes.Destination,
                "Aucune destination n'est définie pour cette partie du composant.",
                component.Id,
                part,
                destinationId));
            return;
        }

        generatedDestinationIds?.Add(destinationId.Value);
    }

    private static void AddFile(
        PublishPlanDestination destination,
        string sourcePath,
        List<PublishLayoutFile> files,
        HashSet<(Guid DestinationId, string DestinationPath)> usedPaths,
        List<PublishLayoutIssue> issues,
        Guid? componentId = null,
        PublishPart? part = null)
    {
        var destinationPath = destination.BasePath.Length == 0
            ? sourcePath
            : string.Concat(destination.BasePath, "/", sourcePath);
        if (!usedPaths.Add((destination.Id, destinationPath)))
        {
            issues.Add(new PublishLayoutIssue(
                PublishValidationCodes.Destination,
                "Deux fichiers logiques produisent le même dossier dans le dépôt.",
                componentId,
                part,
                destination.Id));
            return;
        }

        files.Add(new PublishLayoutFile(destination.Id, sourcePath, destinationPath));
    }

    private static string? NormalizeLogicalPath(string? path)
    {
        var normalized = (path ?? string.Empty).Replace('\\', '/');
        if (normalized.Length == 0
            || normalized.StartsWith('/')
            || (normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':'))
        {
            return null;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            return null;
        }

        return string.Join('/', segments);
    }

    private static bool TryGetGeneratedPart(string segment, out PublishPart part)
    {
        if (string.Equals(segment, "infra", StringComparison.Ordinal))
        {
            part = PublishPart.Infrastructure;
            return true;
        }

        if (string.Equals(segment, "apps", StringComparison.Ordinal))
        {
            part = PublishPart.Applications;
            return true;
        }

        part = default;
        return false;
    }
}
