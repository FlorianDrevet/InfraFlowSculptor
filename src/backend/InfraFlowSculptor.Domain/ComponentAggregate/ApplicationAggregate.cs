using System.Text.RegularExpressions;
using ErrorOr;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Domain.ComponentAggregate;

[AuditedObject(ObjectType.Application, nameof(ApplicationAggregate.Id), nameof(ApplicationAggregate.OrganizationId))]
public sealed partial class ApplicationAggregate : AggregateRoot<Guid>, IOrganizationOwned, IHasVersion
{
    private readonly List<ApplicationStepDefinition> steps = [];

    public ProjectId ProjectId { get; private set; }

    public OrganizationId OrganizationId { get; private set; }

    public ComponentId ComponentId { get; private set; }

    public Guid ResourceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NameKey { get; private set; } = string.Empty;

    public string CodeSourcePath { get; private set; } = string.Empty;

    public string DockerfilePath { get; private set; } = string.Empty;

    public string BuildContextPath { get; private set; } = string.Empty;

    public string ImageRepository { get; private set; } = string.Empty;

    public string? HealthCheckPath { get; private set; }

    public DeploymentStrategyKind DeploymentStrategy { get; private set; }

    public Guid? BuildRegistryEnvironmentId { get; private set; }

    public IReadOnlyCollection<ApplicationStepDefinition> Steps => steps.AsReadOnly();

    public int Version { get; private set; } = 1;

    private ApplicationAggregate()
    {
    }

    private ApplicationAggregate(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        ComponentId componentId,
        Guid resourceId) : base(id)
    {
        ProjectId = projectId;
        OrganizationId = organizationId;
        ComponentId = componentId;
        ResourceId = resourceId;
    }

    public static ErrorOr<ApplicationAggregate> Create(
        Guid id,
        ProjectId projectId,
        OrganizationId organizationId,
        ComponentId componentId,
        Guid resourceId,
        ApplicationDefinition definition,
        IReadOnlyCollection<ApplicationAggregate>? existingApplications = null)
    {
        if (id == Guid.Empty || projectId.Value == Guid.Empty || organizationId.Value == Guid.Empty
            || componentId.Value == Guid.Empty || resourceId == Guid.Empty)
        {
            return Error.Validation("APPLICATION_IDENTITY_REQUIRED", "An application, project, component, resource, and organization are required.");
        }

        var normalized = Normalize(definition);
        var validation = Validate(normalized, componentId, existingApplications ?? []);
        if (validation.IsError)
        {
            return validation.Errors;
        }

        var application = new ApplicationAggregate(id, projectId, organizationId, componentId, resourceId);
        application.Apply(normalized);
        return application;
    }

    public ErrorOr<Success> Update(
        ApplicationDefinition definition,
        IReadOnlyCollection<ApplicationAggregate>? existingApplications = null)
    {
        var normalized = Normalize(definition);
        var validation = Validate(
            normalized,
            ComponentId,
            (existingApplications ?? []).Where(application => application.Id != Id).ToArray());
        if (validation.IsError)
        {
            return validation.Errors;
        }

        Apply(normalized);
        Touch();
        return Result.Success;
    }

    private static ApplicationDefinition Normalize(ApplicationDefinition definition) => definition with
    {
        Name = definition.Name?.Trim() ?? string.Empty,
        CodeSourcePath = NormalizePath(definition.CodeSourcePath),
        DockerfilePath = NormalizePath(definition.DockerfilePath),
        BuildContextPath = NormalizePath(definition.BuildContextPath),
        ImageRepository = definition.ImageRepository?.Trim() ?? string.Empty,
        HealthCheckPath = string.IsNullOrWhiteSpace(definition.HealthCheckPath)
            ? null
            : definition.HealthCheckPath.Trim()
    };

    private static string NormalizePath(string value) => value?.Trim().Replace('\\', '/') ?? string.Empty;

    private static ErrorOr<Success> Validate(
        ApplicationDefinition definition,
        ComponentId componentId,
        IReadOnlyCollection<ApplicationAggregate> existingApplications)
    {
        if (!ApplicationNamePattern().IsMatch(definition.Name))
        {
            return Error.Validation("APPLICATION_NAME_INVALID", "An application name must match ^[a-z][a-z0-9-]{1,39}$.");
        }

        if (!IsRelativePath(definition.CodeSourcePath)
            || !IsRelativePath(definition.DockerfilePath)
            || !IsRelativePath(definition.BuildContextPath))
        {
            return Error.Validation("APPLICATION_PATH_INVALID", "Application source, Dockerfile, and build context must be relative repository paths without parent-directory segments.");
        }

        if (definition.ImageRepository.Length is < 1 or > 256
            || !ImageRepositoryPattern().IsMatch(definition.ImageRepository))
        {
            return Error.Validation("APPLICATION_IMAGE_REPOSITORY_INVALID", "The image repository must match ^[a-z0-9]+([._/-][a-z0-9]+)*$.");
        }

        if (definition.HealthCheckPath is { } healthCheckPath
            && (healthCheckPath.Length > 2048 || healthCheckPath[0] != '/'
                || healthCheckPath.Any(char.IsControl)
                || healthCheckPath.Contains('\\')
                || healthCheckPath.Contains('?')
                || healthCheckPath.Contains('#')))
        {
            return Error.Validation("APPLICATION_HEALTH_CHECK_PATH_INVALID", "A health check must be an HTTP path beginning with '/'.");
        }

        if (!Enum.IsDefined(definition.DeploymentStrategy))
        {
            return Error.Validation("APPLICATION_DEPLOYMENT_STRATEGY_INVALID", "The deployment strategy is unsupported.");
        }

        if (definition.BuildRegistryEnvironmentId == Guid.Empty)
        {
            return Error.Validation("APPLICATION_BUILD_ENVIRONMENT_INVALID", "A build registry environment must be a valid project environment.");
        }

        var duplicate = existingApplications.FirstOrDefault(application =>
            application.ComponentId == componentId
            && string.Equals(application.Name, definition.Name, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
        {
            return Error.Conflict("APPLICATION_NAME_DUPLICATE", $"An application named '{duplicate.Name}' already exists in this component.");
        }

        if (definition.Steps is null || definition.Steps.Count > 50)
        {
            return Error.Validation("APPLICATION_STEPS_INVALID", "An application must contain at most 50 catalog steps.");
        }

        if (definition.Steps.Select(step => step.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != definition.Steps.Count)
        {
            return Error.Conflict("APPLICATION_STEP_DUPLICATE", "An application cannot contain the same catalog step more than once.");
        }

        foreach (var step in definition.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Name) || step.Name.Length > 64
                || !StepNamePattern().IsMatch(step.Name) || !Enum.IsDefined(step.Position)
                || step.Parameters is null || step.Parameters.Count > 64)
            {
                return Error.Validation("APPLICATION_STEP_INVALID", "An application step must have a valid catalog name, position, and parameter list.");
            }

            if (step.Parameters.Any(parameter => string.IsNullOrWhiteSpace(parameter.Name)
                || parameter.Name.Length > 64
                || !StepNamePattern().IsMatch(parameter.Name)
                || parameter.Value is null
                || parameter.Value.Length > 4096))
            {
                return Error.Validation("APPLICATION_STEP_PARAMETER_INVALID", "Step parameter names must be valid and values cannot exceed 4096 characters.");
            }

            if (step.Parameters.Select(parameter => parameter.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != step.Parameters.Count)
            {
                return Error.Conflict("APPLICATION_STEP_PARAMETER_DUPLICATE", "An application step cannot contain duplicate parameter names.");
            }
        }

        return Result.Success;
    }

    private static bool IsRelativePath(string path) =>
        path.Length is > 0 and <= 512
        && !path.Any(char.IsControl)
        && path[0] != '/'
        && !path.Contains(':')
        && !path.Contains('\\')
        && path.Split('/').All(segment => segment.Length > 0 && segment != "..");

    private void Apply(ApplicationDefinition definition)
    {
        Name = definition.Name;
        NameKey = definition.Name.ToUpperInvariant();
        CodeSourcePath = definition.CodeSourcePath;
        DockerfilePath = definition.DockerfilePath;
        BuildContextPath = definition.BuildContextPath;
        ImageRepository = definition.ImageRepository;
        HealthCheckPath = definition.HealthCheckPath;
        DeploymentStrategy = definition.DeploymentStrategy;
        BuildRegistryEnvironmentId = definition.BuildRegistryEnvironmentId;
        steps.Clear();
        steps.AddRange(definition.Steps);
    }

    private void Touch() => Version = checked(Version + 1);

    [GeneratedRegex("^[a-z][a-z0-9-]{1,39}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ApplicationNamePattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex StepNamePattern();

    [GeneratedRegex("^[a-z0-9]+([._/-][a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ImageRepositoryPattern();
}
