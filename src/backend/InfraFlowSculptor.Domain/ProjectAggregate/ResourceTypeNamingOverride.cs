using System.Text.RegularExpressions;
using ErrorOr;

namespace InfraFlowSculptor.Domain.ProjectAggregate;

public sealed partial class ResourceTypeNamingOverride
{
    public string Type { get; private set; } = string.Empty;

    public string? Template { get; private set; }

    public string? Abbreviation { get; private set; }

    private ResourceTypeNamingOverride()
    {
    }

    private ResourceTypeNamingOverride(string type, string? template, string? abbreviation)
    {
        Type = type;
        Template = template;
        Abbreviation = abbreviation;
    }

    internal void SetValues(string? template, string? abbreviation)
    {
        Template = template;
        Abbreviation = abbreviation;
    }

    public static ErrorOr<ResourceTypeNamingOverride> Create(string type, string? template, string? abbreviation)
    {
        var normalizedType = type?.Trim() ?? string.Empty;
        var normalizedTemplate = template?.Trim();
        var normalizedAbbreviation = abbreviation?.Trim().ToLowerInvariant();

        if (!ResourceTypePattern().IsMatch(normalizedType))
        {
            return Error.Validation("NAMING_RESOURCE_TYPE_INVALID", "A naming override requires a valid resource type identifier.");
        }

        if (normalizedTemplate is not null && normalizedTemplate.Length is < 1 or > 200)
        {
            return Error.Validation("PROJECT_NAMING_TEMPLATE_INVALID", "A naming template must contain between 1 and 200 characters.");
        }

        if (normalizedAbbreviation is not null && !AbbreviationPattern().IsMatch(normalizedAbbreviation))
        {
            return Error.Validation("NAMING_ABBREVIATION_INVALID", "An abbreviation must start with a lowercase letter and contain at most 16 lowercase letters or digits.");
        }

        return new ResourceTypeNamingOverride(normalizedType, normalizedTemplate, normalizedAbbreviation);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex ResourceTypePattern();

    [GeneratedRegex("^[a-z][a-z0-9]{0,15}$", RegexOptions.CultureInvariant)]
    private static partial Regex AbbreviationPattern();
}
