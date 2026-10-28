using System.Globalization;
using System.Text.RegularExpressions;
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

    private static readonly Regex PlaceholderPattern = new(
        @"\{\{(?<name>organizationName|invitationUrl|expiresAt)\}\}",
        RegexOptions.CultureInvariant);

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

        if (organizationName.Any(char.IsControl))
        {
            throw new ArgumentException("The organization name cannot contain control characters.", nameof(organizationName));
        }

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
        var htmlBody = ReplacePlaceholders(
            ReadResource(resourceNames.HtmlResource),
            organizationName,
            invitationUrl.AbsoluteUri,
            expiresAtText,
            HtmlEncoder.Default.Encode);
        var textBody = ReplacePlaceholders(
            ReadResource(resourceNames.TextResource),
            organizationName,
            invitationUrl.AbsoluteUri,
            expiresAtText,
            value => value);
        var subject = ReplacePlaceholders(
            resourceNames.Subject,
            organizationName,
            invitationUrl.AbsoluteUri,
            expiresAtText,
            value => value);

        return new RenderedEmailTemplate(subject, htmlBody, textBody);
    }

    private static string ReadResource(string resourceName)
    {
        using var stream = typeof(InvitationEmailTemplateRenderer).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The email template resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ReplacePlaceholders(
        string template,
        string organizationName,
        string invitationUrl,
        string expiresAt,
        Func<string, string> encode) =>
        PlaceholderPattern.Replace(template, match => match.Groups["name"].Value switch
        {
            "organizationName" => encode(organizationName),
            "invitationUrl" => encode(invitationUrl),
            "expiresAt" => encode(expiresAt),
            _ => match.Value
        });
}
