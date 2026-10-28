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

    [Fact]
    public void ProjectFavoritesAreUniqueAndCanBeRemoved()
    {
        var created = UserProfile.Create(
            new UserProfileId(Guid.NewGuid()),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Alice Martin",
            null,
            DateTimeOffset.UtcNow);
        var projectId = new ProjectId(Guid.NewGuid());

        var added = created.Value.SetProjectFavorite(projectId, true);
        var addedAgain = created.Value.SetProjectFavorite(projectId, true);

        Assert.False(added.IsError);
        Assert.False(addedAgain.IsError);
        Assert.Single(created.Value.FavoriteProjects);
        Assert.Equal(projectId, created.Value.FavoriteProjects.Single().ProjectId);

        var removed = created.Value.SetProjectFavorite(projectId, false);

        Assert.False(removed.IsError);
        Assert.Empty(created.Value.FavoriteProjects);
    }

    [Fact]
    public void ProjectFavoriteRejectsAnEmptyProjectId()
    {
        var created = UserProfile.Create(
            new UserProfileId(Guid.NewGuid()),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Alice Martin",
            null,
            DateTimeOffset.UtcNow);

        var result = created.Value.SetProjectFavorite(new ProjectId(Guid.Empty), true);

        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "PROJECT_FAVORITE_ID_INVALID");
    }
}
