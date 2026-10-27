using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;

namespace InfraFlowSculptor.Infrastructure.Email;

public sealed class InvitationEmailTemplateRenderer
{
    private const string French = "fr";
    private const string English = "en";
    private const string OrganizationNamePlaceholder = "{{organizationName}}";
    private const string InvitationUrlPlaceholder = "{{invitationUrl}}";
    private const string ExpirationPlaceholder = "{{expiresAt}}";

    private static readonly Dictionary<string, (string Subject, string HtmlResource, string TextResource)> Resources =
        new Dictionary<string, (string Subject, string HtmlResource, string TextResource)>(StringComparer.Ordinal)
        {
            [French] = (
                "Invitation à rejoindre {{organizationName}}",
                "InfraFlowSculptor.Infrastructure.Email.Templates.Invitation.fr.html",
                "InfraFlowSculptor.Infrastructure.Email.Templates.Invitation.fr.txt"),
            [English] = (
                "Invitation to join {{organizationName}}",
                "InfraFlowSculptor.Infrastructure.Email.Templates.Invitation.en.html",
                "InfraFlowSculptor.Infrastructure.Email.Templates.Invitation.en.txt")
        };

    public static RenderedEmailTemplate Render(
        string language,
        string organizationName,
        Uri invitationUrl,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationName);
        ArgumentNullException.ThrowIfNull(invitationUrl);

        if (!invitationUrl.IsAbsoluteUri
            || (invitationUrl.Scheme != Uri.UriSchemeHttp && invitationUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The invitation URL must be an absolute HTTP or HTTPS URI.", nameof(invitationUrl));
        }

        if (!Resources.TryGetValue(language, out var resourceNames))
        {
            throw new NotSupportedException($"Invitation emails are not available in language '{language}'.");
        }

        var expiresAtText = expiresAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var htmlBody = ReadResource(resourceNames.HtmlResource)
            .Replace(OrganizationNamePlaceholder, HtmlEncoder.Default.Encode(organizationName), StringComparison.Ordinal)
            .Replace(InvitationUrlPlaceholder, HtmlEncoder.Default.Encode(invitationUrl.AbsoluteUri), StringComparison.Ordinal)
            .Replace(ExpirationPlaceholder, HtmlEncoder.Default.Encode(expiresAtText), StringComparison.Ordinal);
        var textBody = ReadResource(resourceNames.TextResource)
            .Replace(OrganizationNamePlaceholder, organizationName, StringComparison.Ordinal)
            .Replace(InvitationUrlPlaceholder, invitationUrl.AbsoluteUri, StringComparison.Ordinal)
            .Replace(ExpirationPlaceholder, expiresAtText, StringComparison.Ordinal);
        var subject = resourceNames.Subject.Replace(
            OrganizationNamePlaceholder,
            organizationName,
            StringComparison.Ordinal);

        return new RenderedEmailTemplate(subject, htmlBody, textBody);
    }

    private static string ReadResource(string resourceName)
    {
        using var stream = typeof(InvitationEmailTemplateRenderer).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The email template resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
