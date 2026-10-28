using InfraFlowSculptor.Infrastructure.Email;

namespace InfraFlowSculptor.Infrastructure.Tests.Email;

public sealed class InvitationEmailTemplateRendererTests
{
    private static readonly DateTimeOffset Expiration = new(2026, 10, 30, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RenderFrenchInvitationIncludesOrganizationLinkAndExpiration()
    {
        var result = InvitationEmailTemplateRenderer.Render(
            "fr",
            "<Contoso & fils>",
            new Uri("https://ifs.example/invitations/token-value"),
            Expiration);

        Assert.Contains("<Contoso & fils>", result.Subject, StringComparison.Ordinal);
        Assert.Contains("&lt;Contoso &amp; fils&gt;", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("https://ifs.example/invitations/token-value", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("2026", result.TextBody, StringComparison.Ordinal);
        Assert.Contains("https://ifs.example/invitations/token-value", result.TextBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void RenderInvitationProvidesHtmlAndPlainText(string language)
    {
        var result = InvitationEmailTemplateRenderer.Render(
            language,
            "Contoso",
            new Uri("https://ifs.example/invitations/token-value"),
            Expiration);

        Assert.False(string.IsNullOrWhiteSpace(result.Subject));
        Assert.False(string.IsNullOrWhiteSpace(result.HtmlBody));
        Assert.False(string.IsNullOrWhiteSpace(result.TextBody));
        Assert.Contains("Contoso", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Contoso", result.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderInvitationRejectsUnsupportedLanguage()
    {
        Assert.Throws<NotSupportedException>(() => InvitationEmailTemplateRenderer.Render(
            "de",
            "Contoso",
            new Uri("https://ifs.example/invitations/token-value"),
            Expiration));
    }

    [Fact]
    public void RenderInvitationRejectsControlCharactersInOrganizationName()
    {
        Assert.Throws<ArgumentException>(() => InvitationEmailTemplateRenderer.Render(
            "en",
            "Contoso\r\nBcc: attacker@example.test",
            new Uri("https://ifs.example/invitations/token-value"),
            Expiration));
    }

    [Fact]
    public void RenderDoesNotReplacePlaceholdersContainedInOrganizationName()
    {
        const string organizationName = "Contoso {{invitationUrl}}";
        var result = InvitationEmailTemplateRenderer.Render(
            "en",
            organizationName,
            new Uri("https://ifs.example/invitations/token-value"),
            Expiration);

        Assert.Contains(organizationName, result.Subject, StringComparison.Ordinal);
        Assert.Contains(organizationName, result.TextBody, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(result.TextBody, "https://ifs.example/invitations/token-value"));
    }

    private static int CountOccurrences(string value, string searchValue) =>
        value.Split(searchValue, StringSplitOptions.None).Length - 1;
}
