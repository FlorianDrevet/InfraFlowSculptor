using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using InfraFlowSculptor.Engine.Model;

namespace InfraFlowSculptor.Engine.Naming;

public static class NameSanitizer
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly ConcurrentDictionary<string, HashSet<char>> AllowedCharacters = new(StringComparer.Ordinal);

    public static NamingSanitizationResult Sanitize(string rawName, NamingRuleSnapshot rule)
    {
        ArgumentNullException.ThrowIfNull(rawName);
        ArgumentNullException.ThrowIfNull(rule);

        var transformations = new List<string>();
        var value = rawName;
        if (string.Equals(rule.Case, "lower", StringComparison.OrdinalIgnoreCase))
        {
            var lower = value.ToLowerInvariant();
            if (!string.Equals(value, lower, StringComparison.Ordinal))
            {
                transformations.Add("Casse mise en minuscules");
                value = lower;
            }
        }
        else if (string.Equals(rule.Case, "upper", StringComparison.OrdinalIgnoreCase))
        {
            var upper = value.ToUpperInvariant();
            if (!string.Equals(value, upper, StringComparison.Ordinal))
            {
                transformations.Add("Casse mise en majuscules");
                value = upper;
            }
        }

        var allowed = AllowedCharacters.GetOrAdd(CacheKey(rule), _ => BuildAllowedCharacters(rule));
        var filtered = new string(value.Where(allowed.Contains).ToArray());
        if (!string.Equals(value, filtered, StringComparison.Ordinal))
        {
            transformations.Add("Caractères interdits retirés");
            value = filtered;
        }

        var separators = GetSeparators(rule.Separators);
        if (rule.ForbidConsecutiveSeparators)
        {
            var collapsed = CollapseSeparators(value, separators);
            if (!string.Equals(value, collapsed, StringComparison.Ordinal))
            {
                transformations.Add("Séparateurs consécutifs réduits");
                value = collapsed;
            }
        }

        var start = 0;
        var end = value.Length;
        while (start < end
               && (separators.Contains(value[start]) || !MatchesCharacterRule(value[start], rule.FirstCharacter)))
        {
            start++;
        }

        while (end > start
               && (separators.Contains(value[end - 1]) || !MatchesCharacterRule(value[end - 1], rule.LastCharacter)))
        {
            end--;
        }

        if (start > 0 || end < value.Length)
        {
            transformations.Add("Caractères de bord retirés");
            value = value[start..end];
        }

        return new NamingSanitizationResult(value, transformations);
    }

    public static bool IsValid(string value, NamingRuleSnapshot rule)
    {
        if (value.Length < rule.MinimumLength || value.Length > rule.MaximumLength
            || value.Length == 0
            || !MatchesCharacterRule(value[0], rule.FirstCharacter)
            || !MatchesCharacterRule(value[^1], rule.LastCharacter)
            || string.Equals(rule.Case, "lower", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal)
            || string.Equals(rule.Case, "upper", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(value, value.ToUpperInvariant(), StringComparison.Ordinal)
            || rule.ForbidConsecutiveSeparators && HasConsecutiveSeparators(value, GetSeparators(rule.Separators)))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(value, rule.AllowedPattern, RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static HashSet<char> BuildAllowedCharacters(NamingRuleSnapshot rule)
    {
        var length = Math.Min(rule.MaximumLength, Math.Max(rule.MinimumLength, 3));
        var values = new HashSet<char>();
        for (var code = 32; code <= 126; code++)
        {
            var candidate = NormalizeCase((char)code, rule.Case);
            var sample = Enumerable.Repeat(NormalizeCase('a', rule.Case), length).ToArray();
            var index = length > 2 ? 1 : 0;
            sample[index] = candidate;
            if (MatchesPattern(new string(sample), rule.AllowedPattern))
            {
                values.Add(candidate);
            }
        }

        return values;
    }

    private static char NormalizeCase(char value, string casing) =>
        string.Equals(casing, "lower", StringComparison.OrdinalIgnoreCase)
            ? char.ToLowerInvariant(value)
            : string.Equals(casing, "upper", StringComparison.OrdinalIgnoreCase)
                ? char.ToUpperInvariant(value)
                : value;

    private static string CollapseSeparators(string value, HashSet<char> separators)
    {
        if (separators.Count == 0)
        {
            return value;
        }

        var result = new System.Text.StringBuilder(value.Length);
        var previousWasSeparator = false;
        foreach (var character in value)
        {
            var isSeparator = separators.Contains(character);
            if (!isSeparator || !previousWasSeparator)
            {
                result.Append(character);
            }

            previousWasSeparator = isSeparator;
        }

        return result.ToString();
    }

    private static HashSet<char> GetSeparators(IReadOnlyList<string> separators) => separators
        .Where(separator => separator.Length == 1)
        .Select(separator => separator[0])
        .ToHashSet();

    private static bool HasConsecutiveSeparators(string value, HashSet<char> separators)
    {
        for (var index = 1; index < value.Length; index++)
        {
            if (separators.Contains(value[index - 1]) && separators.Contains(value[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesCharacterRule(char value, string rule) =>
        rule switch
        {
            "letter" => char.IsAsciiLetter(value),
            "alphanumeric" => char.IsAsciiLetterOrDigit(value),
            "notDot" => value != '.',
            "any" => true,
            _ => false
        };

    private static bool MatchesPattern(string value, string pattern)
    {
        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static string CacheKey(NamingRuleSnapshot rule) => string.Join(
        '\u001f',
        rule.AllowedPattern,
        rule.Case,
        rule.MinimumLength,
        rule.MaximumLength,
        rule.FirstCharacter,
        rule.LastCharacter);
}
