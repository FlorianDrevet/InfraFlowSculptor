using ErrorOr;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.UserProfileAggregate;

public sealed class UserProfile : AggregateRoot<UserProfileId>
{
    public const string DefaultLanguage = "fr";
    public const string DefaultTheme = "system";

    public Guid TenantId { get; private set; }

    public Guid ObjectId { get; private set; }

    public UserKey UserKey => new(TenantId, ObjectId);

    public string DisplayName { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string PreferredLanguage { get; private set; } = DefaultLanguage;

    public string PreferredTheme { get; private set; } = DefaultTheme;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    private UserProfile()
    {
    }

    private UserProfile(
        UserProfileId id,
        UserKey userKey,
        string displayName,
        string? email,
        DateTimeOffset now) : base(id)
    {
        TenantId = userKey.TenantId;
        ObjectId = userKey.ObjectId;
        DisplayName = displayName;
        Email = email;
        CreatedAt = now;
        LastSeenAt = now;
    }

    public static ErrorOr<UserProfile> Create(
        UserProfileId id,
        UserKey userKey,
        string displayName,
        string? email,
        DateTimeOffset now)
    {
        if (id.Value == Guid.Empty || userKey.IsEmpty)
        {
            return Error.Validation("USER_PROFILE_KEY_INVALID", "A stable tenant and object identifier is required.");
        }

        return new UserProfile(id, userKey, NormalizeDisplayName(displayName), email, now);
    }

    public void SynchronizeIdentity(string displayName, string? email, DateTimeOffset lastSeenAt)
    {
        DisplayName = NormalizeDisplayName(displayName);
        Email = email;
        LastSeenAt = lastSeenAt;
    }

    public ErrorOr<Success> SetPreferences(string language, string theme)
    {
        if (language is not ("fr" or "en"))
        {
            return Error.Validation("USER_LANGUAGE_INVALID", "Preferred language must be 'fr' or 'en'.");
        }

        if (theme is not ("system" or "dark" or "light"))
        {
            return Error.Validation("USER_THEME_INVALID", "Preferred theme must be 'system', 'dark' or 'light'.");
        }

        PreferredLanguage = language;
        PreferredTheme = theme;
        return Result.Success;
    }

    private static string NormalizeDisplayName(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? "IFS user" : displayName.Trim();
}
