using System.Text.RegularExpressions;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Naming;

public sealed partial class NamingEngine
{
    private static readonly HashSet<string> SupportedTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "project", "component", "name", "abbr", "env", "region"
    };

    private static readonly string SupportedTokenList = string.Join(", ", SupportedTokens.Order(StringComparer.Ordinal));

    public static NamingPreviewResult Preview(ModelSnapshot snapshot)
    {
        var issues = new List<NamingIssue>();
        ValidateTemplate(snapshot.Naming.DefaultTemplate, issues);
        ValidateTemplate(snapshot.Naming.ResourceGroupTemplate, issues);
        if (issues.Count > 0)
        {
            return new NamingPreviewResult([], issues);
        }

        var items = new List<NamingPreviewItem>(snapshot.Environments.Count);
        foreach (var environment in snapshot.Environments)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["project"] = snapshot.Project.Code,
                ["component"] = "main",
                ["name"] = "main",
                ["abbr"] = "kv",
                ["env"] = environment.Code,
                ["region"] = environment.Region
            };
            items.Add(new NamingPreviewItem(
                environment.Code,
                Render(snapshot.Naming.DefaultTemplate, values),
                Render(snapshot.Naming.ResourceGroupTemplate, values)));
        }

        return new NamingPreviewResult(items, []);
    }

    private static void ValidateTemplate(string template, List<NamingIssue> issues)
    {
        if (template.Length > 200)
        {
            issues.Add(new NamingIssue("NAMING_TEMPLATE_LENGTH", "A naming template cannot exceed 200 characters."));
        }

        var tokens = TokenPattern().Matches(template)
            .Select(match => match.Groups["name"].Value)
            .ToArray();
        var unknownTokens = tokens
            .Where(token => !SupportedTokens.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unknownTokens.Length > 0)
        {
            issues.Add(new NamingIssue(
                "NAMING_TOKEN_UNKNOWN",
                $"Unknown naming token(s): {string.Join(", ", unknownTokens)}. Supported tokens: {SupportedTokenList}."));
        }

        if (!tokens.Contains("name", StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new NamingIssue("NAMING_NAME_TOKEN_REQUIRED", "A naming template must contain the {name} token."));
        }

        var literalText = TokenPattern().Replace(template, string.Empty);
        if (literalText.Contains('{', StringComparison.Ordinal)
            || literalText.Contains('}', StringComparison.Ordinal)
            || !TemplateLiteralPattern().IsMatch(literalText))
        {
            issues.Add(new NamingIssue(
                "NAMING_TEMPLATE_FORMAT",
                "Outside tokens, naming templates may contain only lowercase letters, digits, dashes, underscores, and dots."));
        }
    }

    private static string Render(string template, Dictionary<string, string> values) =>
        TokenPattern().Replace(template, match => values[match.Groups["name"].Value]);

    [GeneratedRegex("\\{(?<name>[^{}]+)\\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    [GeneratedRegex("^[a-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateLiteralPattern();
}
