namespace InfraFlowSculptor.Engine.Model;

public sealed record NamingRuleSnapshot(
    string Abbreviation,
    string AllowedPattern,
    string Case,
    IReadOnlyList<string> Separators,
    bool ForbidConsecutiveSeparators,
    int MinimumLength,
    int MaximumLength,
    string FirstCharacter,
    string LastCharacter,
    string Uniqueness,
    string? AvailabilityDomain)
{
    public const string GlobalUniqueness = "global";
}
