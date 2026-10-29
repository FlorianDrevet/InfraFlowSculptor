using System.Text.RegularExpressions;

namespace InfraFlowSculptor.Domain.ComponentAggregate;

public static partial class SecretValueDetector
{
    private static readonly Regex[] KnownTokenPatterns =
    [
        GitHubTokenPattern(),
        GitLabTokenPattern(),
        SlackTokenPattern(),
        AwsAccessKeyPattern(),
        GoogleApiKeyPattern(),
        JsonWebTokenPattern()
    ];

    private static readonly Regex ConnectionSecretPattern = new(
        @"(?ix)(?:\b(?:password|pwd|accountkey|sharedaccesskey|clientsecret|client_secret|connectionstring)\s*=\s*[^;\s&]+|\bsig\s*=\s*[^&\s]+|://[^/\s:@]+:[^@\s]+@)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SecretLikeNamePattern = new(
        @"(?i)(password|secret|token|api.?key|connection.?string)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool HasSecretLikeName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && SecretLikeNamePattern.IsMatch(name);

    public static bool IsSuspicious(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (KnownTokenPatterns.Any(pattern => pattern.IsMatch(value)) || ConnectionSecretPattern.IsMatch(value))
        {
            return true;
        }

        return value.Length >= 20 && CalculateShannonEntropy(value) > 4.0;
    }

    private static double CalculateShannonEntropy(string value) => value
        .GroupBy(character => character)
        .Select(group => (double)group.Count() / value.Length)
        .Sum(probability => -probability * Math.Log2(probability));

    [GeneratedRegex(@"\b(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{20,}\b|\bgithub_pat_[A-Za-z0-9_]{20,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GitHubTokenPattern();

    [GeneratedRegex(@"\bglpat-[A-Za-z0-9_-]{20,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GitLabTokenPattern();

    [GeneratedRegex(@"\bxox[baprs]-[A-Za-z0-9-]{10,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SlackTokenPattern();

    [GeneratedRegex(@"\bAKIA[0-9A-Z]{16}\b", RegexOptions.CultureInvariant)]
    private static partial Regex AwsAccessKeyPattern();

    [GeneratedRegex(@"\bAIza[0-9A-Za-z_-]{35}\b", RegexOptions.CultureInvariant)]
    private static partial Regex GoogleApiKeyPattern();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex JsonWebTokenPattern();
}
