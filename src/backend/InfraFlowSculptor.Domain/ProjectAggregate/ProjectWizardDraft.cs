using System.Text.Json;
using ErrorOr;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.ProjectAggregate;

public sealed class ProjectWizardDraft : IOrganizationOwned
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public OrganizationId OrganizationId { get; private set; }

    public UserProfileId UserProfileId { get; private set; }

    public string ContentJson { get; private set; } = "{}";

    public DateTimeOffset ExpiresAt { get; private set; }

    private ProjectWizardDraft()
    {
    }

    private ProjectWizardDraft(
        OrganizationId organizationId,
        UserProfileId userProfileId,
        string contentJson,
        DateTimeOffset expiresAt)
    {
        OrganizationId = organizationId;
        UserProfileId = userProfileId;
        ContentJson = contentJson;
        ExpiresAt = expiresAt;
    }

    public static ErrorOr<ProjectWizardDraft> Create(
        OrganizationId organizationId,
        UserProfileId userProfileId,
        string contentJson,
        DateTimeOffset now)
    {
        if (organizationId.Value == Guid.Empty || userProfileId.Value == Guid.Empty)
        {
            return Error.Validation("PROJECT_WIZARD_DRAFT_IDENTITY_REQUIRED", "A user and organization are required for a project wizard draft.");
        }

        var validContent = ValidateContent(contentJson);
        if (validContent.IsError)
        {
            return validContent.Errors;
        }

        return new ProjectWizardDraft(organizationId, userProfileId, validContent.Value, now.Add(Lifetime));
    }

    public ErrorOr<Success> Save(string contentJson, DateTimeOffset now)
    {
        var validContent = ValidateContent(contentJson);
        if (validContent.IsError)
        {
            return validContent.Errors;
        }

        ContentJson = validContent.Value;
        ExpiresAt = now.Add(Lifetime);
        return Result.Success;
    }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    private static ErrorOr<string> ValidateContent(string? contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return Error.Validation("PROJECT_WIZARD_DRAFT_JSON_INVALID", "A project wizard draft must contain a JSON object.");
        }

        try
        {
            using var document = JsonDocument.Parse(contentJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? contentJson
                : Error.Validation("PROJECT_WIZARD_DRAFT_JSON_INVALID", "A project wizard draft must contain a JSON object.");
        }
        catch (JsonException)
        {
            return Error.Validation("PROJECT_WIZARD_DRAFT_JSON_INVALID", "A project wizard draft must contain valid JSON.");
        }
    }
}
