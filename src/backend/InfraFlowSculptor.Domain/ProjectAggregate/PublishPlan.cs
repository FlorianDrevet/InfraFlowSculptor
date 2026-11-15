using ErrorOr;

namespace InfraFlowSculptor.Domain.ProjectAggregate;

public sealed class PublishPlan
{
    private readonly List<PublishPlanDestination> destinations = [];
    private readonly List<PublishPlanAssignment> assignments = [];

    private PublishPlan()
    {
    }

    public PublishPreset Preset { get; private set; } = PublishPreset.MonoRepo;

    public Guid? DefaultInfrastructureDestinationId { get; private set; }

    public Guid? DefaultApplicationsDestinationId { get; private set; }

    public Guid? DefaultSourceCodeDestinationId { get; private set; }

    public IReadOnlyCollection<PublishPlanDestination> Destinations => destinations.AsReadOnly();

    public IReadOnlyCollection<PublishPlanAssignment> Assignments => assignments.AsReadOnly();

    public static PublishPlan CreateDefault() => new();

    public ErrorOr<Success> ConfigurePreset(string? preset)
    {
        var normalizedPreset = string.IsNullOrWhiteSpace(preset) ? nameof(PublishPreset.MonoRepo) : preset.Trim();
        if (!Enum.TryParse<PublishPreset>(normalizedPreset, ignoreCase: false, out var parsedPreset)
            || !Enum.IsDefined(parsedPreset))
        {
            return Error.Validation(PublishValidationCodes.PresetInvalid, "Le préréglage de publication n'est pas reconnu.");
        }

        Preset = parsedPreset;
        return Result.Success;
    }

    public ErrorOr<Success> Configure(PublishPlanConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!Enum.IsDefined(configuration.Preset))
        {
            return Error.Validation(PublishValidationCodes.Destination, "Le préréglage de publication n'est pas reconnu.");
        }

        var configuredDestinations = new List<PublishPlanDestination>();
        foreach (var input in configuration.Destinations)
        {
            var destination = PublishPlanDestination.Create(input);
            if (destination.IsError)
            {
                return destination.Errors;
            }

            configuredDestinations.Add(destination.Value);
        }

        if (configuredDestinations.Select(destination => destination.Id).Distinct().Count() != configuredDestinations.Count)
        {
            return Error.Validation(PublishValidationCodes.Destination, "Chaque destination doit avoir un identifiant distinct.");
        }

        var destinationIds = configuredDestinations.Select(destination => destination.Id).ToHashSet();
        if (!IsDestinationReferenceValid(configuration.DefaultInfrastructureDestinationId, destinationIds)
            || !IsDestinationReferenceValid(configuration.DefaultApplicationsDestinationId, destinationIds)
            || !IsDestinationReferenceValid(configuration.DefaultSourceCodeDestinationId, destinationIds))
        {
            return Error.Validation(PublishValidationCodes.Destination, "Une règle par défaut pointe vers une destination absente.");
        }

        var defaultRuleError = ValidatePresetDefaults(configuration);
        if (defaultRuleError is not null)
        {
            return defaultRuleError.Value;
        }

        var overlapError = ValidateDestinationBasePaths(configuredDestinations);
        if (overlapError is not null)
        {
            return overlapError.Value;
        }

        var configuredAssignments = new List<PublishPlanAssignment>();
        foreach (var input in configuration.Assignments)
        {
            if (input.ComponentId == Guid.Empty
                || !Enum.IsDefined(input.Part)
                || !destinationIds.Contains(input.DestinationId))
            {
                return Error.Validation(PublishValidationCodes.Destination, "Une partie doit viser un composant et une destination existants.");
            }

            configuredAssignments.Add(new PublishPlanAssignment(input.ComponentId, input.Part, input.DestinationId));
        }

        if (configuredAssignments
            .Select(assignment => (assignment.ComponentId, assignment.Part))
            .Distinct()
            .Count() != configuredAssignments.Count)
        {
            return Error.Validation(PublishValidationCodes.Destination, "Une partie ne peut avoir qu'une destination par composant.");
        }

        Preset = configuration.Preset;
        DefaultInfrastructureDestinationId = configuration.DefaultInfrastructureDestinationId;
        DefaultApplicationsDestinationId = configuration.DefaultApplicationsDestinationId;
        DefaultSourceCodeDestinationId = configuration.DefaultSourceCodeDestinationId;
        destinations.Clear();
        destinations.AddRange(configuredDestinations);
        assignments.Clear();
        assignments.AddRange(configuredAssignments);
        return Result.Success;
    }

    public Guid? ResolveDestination(Guid componentId, PublishPart part)
    {
        var assignment = assignments.SingleOrDefault(candidate =>
            candidate.ComponentId == componentId && candidate.Part == part);
        if (assignment is not null)
        {
            return assignment.DestinationId;
        }

        return part switch
        {
            PublishPart.Infrastructure => DefaultInfrastructureDestinationId,
            PublishPart.Applications => DefaultApplicationsDestinationId,
            PublishPart.SourceCode => DefaultSourceCodeDestinationId,
            _ => null
        };
    }

    private static Error? ValidatePresetDefaults(PublishPlanConfiguration configuration)
    {
        if (configuration.Preset == PublishPreset.MonoRepo
            && new[]
            {
                configuration.DefaultInfrastructureDestinationId,
                configuration.DefaultApplicationsDestinationId,
                configuration.DefaultSourceCodeDestinationId
            }.Where(id => id.HasValue).Distinct().Count() > 1)
        {
            return Error.Validation(PublishValidationCodes.Destination, "Le préréglage mono-dépôt utilise une même destination par défaut.");
        }

        if (configuration.Preset == PublishPreset.InfrastructureAndCodeSeparated
            && configuration.DefaultApplicationsDestinationId is { } applicationsDestinationId
            && configuration.DefaultSourceCodeDestinationId is { } sourceCodeDestinationId
            && applicationsDestinationId != sourceCodeDestinationId)
        {
            return Error.Validation(PublishValidationCodes.Destination, "Les applications et leur code source utilisent la même destination par défaut.");
        }

        if (configuration.Preset == PublishPreset.OneRepositoryPerComponent
            && (configuration.DefaultInfrastructureDestinationId is not null
                || configuration.DefaultApplicationsDestinationId is not null
                || configuration.DefaultSourceCodeDestinationId is not null))
        {
            return Error.Validation(PublishValidationCodes.Destination, "Le préréglage par composant attend une destination désignée pour chaque composant.");
        }

        return null;
    }

    private static Error? ValidateDestinationBasePaths(List<PublishPlanDestination> configuredDestinations)
    {
        for (var firstIndex = 0; firstIndex < configuredDestinations.Count; firstIndex++)
        {
            var first = configuredDestinations[firstIndex];
            for (var secondIndex = firstIndex + 1; secondIndex < configuredDestinations.Count; secondIndex++)
            {
                var second = configuredDestinations[secondIndex];
                if (first.GitConnectionId == second.GitConnectionId
                    && string.Equals(first.ProviderRepositoryId, second.ProviderRepositoryId, StringComparison.OrdinalIgnoreCase)
                    && BasePathsOverlap(first.BasePath, second.BasePath))
                {
                    return Error.Validation(
                        PublishValidationCodes.Destination,
                        "Deux destinations du même dépôt ne peuvent pas avoir des chemins de base identiques ou imbriqués.");
                }
            }
        }

        return null;
    }

    private static bool BasePathsOverlap(string first, string second) =>
        string.Equals(first, second, StringComparison.Ordinal)
        || first.Length == 0
        || second.Length == 0
        || first.StartsWith(string.Concat(second, "/"), StringComparison.Ordinal)
        || second.StartsWith(string.Concat(first, "/"), StringComparison.Ordinal);

    private static bool IsDestinationReferenceValid(Guid? destinationId, HashSet<Guid> destinationIds) =>
        destinationId is null || destinationIds.Contains(destinationId.Value);
}
