using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.UserProfileAggregate;

namespace InfraFlowSculptor.Domain.Tests.Users;

public sealed class UserProfileTests
{
    [Fact]
    public void NewProfileUsesDefaultLanguageAndTheme()
    {
        var created = UserProfile.Create(
            new UserProfileId(Guid.NewGuid()),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Alice Martin",
            "alice@contoso.example",
            DateTimeOffset.UtcNow);

        Assert.False(created.IsError);
        Assert.Equal(UserProfile.DefaultLanguage, created.Value.PreferredLanguage);
        Assert.Equal(UserProfile.DefaultTheme, created.Value.PreferredTheme);
    }

    [Fact]
    public void PreferencesAcceptSupportedLanguageAndTheme()
    {
        var created = UserProfile.Create(
            new UserProfileId(Guid.NewGuid()),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Alice Martin",
            null,
            DateTimeOffset.UtcNow);

        var updated = created.Value.SetPreferences("en", "dark");

        Assert.False(updated.IsError);
        Assert.Equal("en", created.Value.PreferredLanguage);
        Assert.Equal("dark", created.Value.PreferredTheme);
    }
}
