using System.Text.Json;
using System.Text.RegularExpressions;
using ErrorOr;
using InfraFlowSculptor.Catalog;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Application.Components.Commands;

internal static partial class ResourceCommandSupport
{
    public static async Task<ErrorOr<(ProjectAggregate Project, ComponentAggregate Component)>> LoadAsync(
        IIfsDbContext dbContext,
        ICurrentOrganization currentOrganization,
        Guid projectId,
        Guid componentId,
        CancellationToken cancellationToken)
    {
        if (currentOrganization.Id is not { } organizationId)
        {
            return Error.NotFound("NOT_FOUND", "The project was not found.");
        }

        var project = await dbContext.Query<ProjectAggregate>()
            .Include(candidate => candidate.Environments)
            .SingleOrDefaultAsync(candidate => candidate.Id == new ProjectId(projectId), cancellationToken);
        var component = await dbContext.Query<ComponentAggregate>()
            .SingleOrDefaultAsync(candidate => candidate.Id == new ComponentId(componentId)
                && candidate.ProjectId == new ProjectId(projectId),
                cancellationToken);
        if (project is null || component is null || project.OrganizationId != organizationId)
        {
            return Error.NotFound("COMPONENT_NOT_FOUND", "The component was not found.");
        }

        return (project, component);
    }

    public static ErrorOr<Success> CheckComponentVersion(ComponentAggregate component, int expectedVersion)
    {
        if (component.Version != expectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The component changed. Reload it before saving.",
                new Dictionary<string, object> { ["currentVersion"] = component.Version });
        }

        return Result.Success;
    }

    public static ErrorOr<Success> CheckModelVersion(ProjectAggregate project, int expectedVersion)
    {
        if (project.ModelVersion != expectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The project model changed. Reload it before saving.",
                new Dictionary<string, object> { ["currentVersion"] = project.ModelVersion });
        }

        return Result.Success;
    }

    public static ErrorOr<Success> CheckResourceVersion(Resource resource, int expectedVersion)
    {
        if (resource.Version != expectedVersion)
        {
            return Error.Conflict("CONFLICT_VERSION", "The resource changed. Reload it before saving.",
                new Dictionary<string, object> { ["currentVersion"] = resource.Version });
        }

        return Result.Success;
    }

    public static ErrorOr<IReadOnlyDictionary<string, JsonElement>> ParseProperties(JsonElement properties)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return Error.Validation("RESOURCE_PROPERTIES_OBJECT_REQUIRED", "Resource properties must be a JSON object.");
        }

        var parsed = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in properties.EnumerateObject())
        {
            if (property.Name.Length is < 1 or > 256 || !parsed.TryAdd(property.Name, property.Value.Clone()))
            {
                return Error.Validation("RESOURCE_PROPERTY_NAME_INVALID", "Property names must be unique and contain between 1 and 256 characters.");
            }
        }

        return parsed;
    }

    public static CatalogResourceTypeDescriptor? FindDescriptor(
        ProjectAggregate project,
        string type,
        ICatalogProvider catalogProvider) =>
        catalogProvider.GetVersion(project.CatalogVersion)?.GetResourceType(type);

    public static Guid[] GetTargetIds(ComponentAggregate component, ProjectAggregate project) =>
        component.Mode == ComponentDeploymentMode.PerEnvironment
            ? project.Environments.Where(environment => component.TargetsEnvironment(environment.Id))
                .Select(environment => environment.Id)
                .ToArray()
            : component.SingleTarget is { } target ? [target.Id] : [];

    public static ErrorOr<Success> ValidateForcedName(CatalogResourceTypeDescriptor descriptor, string name)
    {
        var naming = descriptor.Definition.GetProperty("naming");
        var normalized = name?.Trim() ?? string.Empty;
        var pattern = naming.GetProperty("allowed").GetString()!;
        var minimum = naming.GetProperty("minLength").GetInt32();
        var maximum = naming.GetProperty("maxLength").GetInt32();
        if (normalized.Length < minimum || normalized.Length > maximum || !MatchesPattern(normalized, pattern))
        {
            return Error.Validation("RESOURCE_FORCED_NAME_INVALID", "A forced name must satisfy the naming rules in the resource type descriptor.");
        }

        return Result.Success;
    }

    public static ErrorOr<Success> ValidateChildName(
        CatalogResourceTypeDescriptor descriptor,
        string childType,
        string name)
    {
        var child = FindChildDescriptor(descriptor, childType);
        if (child is null)
        {
            return Error.Validation("RESOURCE_CHILD_TYPE_UNKNOWN", $"Child type '{childType}' is not declared by the resource descriptor.");
        }

        var naming = child.Value.GetProperty("naming");
        var normalized = name?.Trim() ?? string.Empty;
        var pattern = naming.GetProperty("allowed").GetString()!;
        var minimum = naming.GetProperty("minLength").GetInt32();
        var maximum = naming.GetProperty("maxLength").GetInt32();
        if (normalized.Length < minimum || normalized.Length > maximum || !MatchesPattern(normalized, pattern))
        {
            return Error.Validation("RESOURCE_CHILD_NAME_INVALID", "A child name must satisfy the naming rules in its descriptor.");
        }

        return Result.Success;
    }

    private static JsonElement? FindChildDescriptor(CatalogResourceTypeDescriptor descriptor, string childType)
    {
        foreach (var child in descriptor.Definition.GetProperty("children").EnumerateArray())
        {
            if (string.Equals(child.GetProperty("type").GetString(), childType, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    public static ErrorOr<Success> ValidateExistingResourceId(CatalogResourceTypeDescriptor descriptor, string resourceId)
    {
        var segments = resourceId?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var expectedType = descriptor.Definition.GetProperty("armType").GetString()!
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (resourceId is null
            || !resourceId.StartsWith('/')
            || segments.Length != 4 + expectedType.Length * 2
            || !string.Equals(segments[0], "subscriptions", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(segments[1], out _)
            || !string.Equals(segments[2], "resourceGroups", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(segments[3])
            || !string.Equals(segments[4], "providers", StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("RESOURCE_EXISTING_ID_INVALID", "Use a complete Azure resource identifier for the selected resource type.");
        }

        var typeSegments = segments.Skip(5).Take(expectedType.Length);
        var nameSegments = segments.Skip(5 + expectedType.Length);
        if (!typeSegments.SequenceEqual(expectedType, StringComparer.OrdinalIgnoreCase)
            || nameSegments.Any(string.IsNullOrWhiteSpace))
        {
            return Error.Validation("RESOURCE_EXISTING_TYPE_MISMATCH", "The Azure resource identifier type must match the selected catalogue type.");
        }

        return Result.Success;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
