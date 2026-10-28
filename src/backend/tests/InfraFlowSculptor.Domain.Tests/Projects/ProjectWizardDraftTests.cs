using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.ProjectAggregate;

namespace InfraFlowSculptor.Domain.Tests.Projects;

public sealed class ProjectWizardDraftTests
{
    [Fact]
    public void CreateScopesDraftToUserAndOrganizationForThirtyDays()
    {
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        var organizationId = new OrganizationId(Guid.NewGuid());
        var userId = new UserProfileId(Guid.NewGuid());

        var result = ProjectWizardDraft.Create(organizationId, userId, "{\"step\":2}", now);

        Assert.False(result.IsError);
        Assert.Equal(organizationId, result.Value.OrganizationId);
        Assert.Equal(userId, result.Value.UserProfileId);
        Assert.Equal("{\"step\":2}", result.Value.ContentJson);
        Assert.Equal(now.AddDays(30), result.Value.ExpiresAt);
    }

    [Fact]
    public void SavingDraftAgainRefreshesItsThirtyDayExpiration()
    {
        var createdAt = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        var draft = ProjectWizardDraft.Create(
            new OrganizationId(Guid.NewGuid()),
            new UserProfileId(Guid.NewGuid()),
            "{\"step\":1}",
            createdAt).Value;
        var savedAt = createdAt.AddDays(12);

        var update = draft.Save("{\"step\":3}", savedAt);

        Assert.False(update.IsError);
        Assert.Equal("{\"step\":3}", draft.ContentJson);
        Assert.Equal(savedAt.AddDays(30), draft.ExpiresAt);
    }

    [Fact]
    public void DraftExpiresAtTheThirtyDayBoundary()
    {
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        var draft = ProjectWizardDraft.Create(
            new OrganizationId(Guid.NewGuid()),
            new UserProfileId(Guid.NewGuid()),
            "{}",
            now).Value;

        Assert.False(draft.IsExpired(now.AddDays(30).AddTicks(-1)));
        Assert.True(draft.IsExpired(now.AddDays(30)));
    }

    [Fact]
    public void DraftRejectsInvalidJson()
    {
        var result = ProjectWizardDraft.Create(
            new OrganizationId(Guid.NewGuid()),
            new UserProfileId(Guid.NewGuid()),
            "not-json",
            DateTimeOffset.UtcNow);

        Assert.Contains(result.Errors, error => error.Code == "PROJECT_WIZARD_DRAFT_JSON_INVALID");
    }
}
