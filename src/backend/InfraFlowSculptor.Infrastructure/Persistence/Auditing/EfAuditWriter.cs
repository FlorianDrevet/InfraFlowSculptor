using System.Text.Json;
using System.Text.Json.Nodes;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Auditing;

public sealed class EfAuditWriter(
    IIfsDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider clock) : IAuditWriter
{
    private const string DisplayNameField = "displayName";
    private const string EmailFieldPrefix = "email.";
    private const string LikeEscapeCharacter = "\\";

    public async Task WriteAsync(AuditedAttribute audit, CancellationToken cancellationToken)
    {
        var changes = dbContext.GetAuditChanges()
            .Where(candidate => candidate.ObjectType == audit.ObjectType)
            .ToArray();
        if (changes.Length == 0)
        {
            return;
        }

        if (!currentUser.IsAuthenticated)
        {
            throw new InvalidOperationException("An audited user action requires an authenticated actor.");
        }

        var actor = await dbContext.Query<UserProfile>()
            .SingleOrDefaultAsync(profile => profile.TenantId == currentUser.Key.TenantId
                && profile.ObjectId == currentUser.Key.ObjectId,
                cancellationToken);
        if (actor is null)
        {
            throw new InvalidOperationException("An audited user action requires a persisted user profile.");
        }

        var via = currentUser.AuthenticationKind == AuthenticationKind.ApiToken
            ? AuditVia.Api
            : AuditVia.Screen;
        foreach (var change in changes)
        {
            var occurredAt = clock.GetUtcNow();
            var eventId = Guid.CreateVersion7(occurredAt);
            var auditEvent = new AuditEventEntity(
                eventId,
                new OrganizationId(change.OrganizationId),
                occurredAt,
                currentUser.AuthenticationKind == AuthenticationKind.ApiToken
                    ? AuditActorKinds.ApiToken
                    : AuditActorKinds.User,
                actor.Id.Value.ToString("D"),
                via,
                audit.Action,
                change.ObjectType,
                change.ObjectId,
                CreateDiff(change.Properties),
                change.ProjectId);
            dbContext.Query<AuditEventEntity>().Add(auditEvent);

            foreach (var property in change.Properties.Where(property => property.PersonalDataField is not null))
            {
                await AddPersonalDataAsync(eventId, actor.Id, property, cancellationToken);
            }

            if (audit.Action == AuditActions.InvitationAccepted && currentUser.VerifiedEmail is { } verifiedEmail)
            {
                await dbContext.Query<AuditPersonalDataEntity>()
                    .IgnoreQueryFilters()
                    .Where(personalData => personalData.Field.StartsWith(EmailFieldPrefix)
                        && EF.Functions.ILike(
                            personalData.Value,
                            EscapeLikePattern(verifiedEmail.Value),
                            LikeEscapeCharacter)
                        && personalData.AuditEvent.OrganizationId == new OrganizationId(change.OrganizationId)
                        && personalData.AuditEvent.ObjectId == change.ObjectId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(personalData => personalData.SubjectUserId, actor.Id),
                        cancellationToken);
            }
        }
    }

    private async Task AddPersonalDataAsync(
        Guid eventId,
        UserProfileId actorId,
        AuditPropertyChange property,
        CancellationToken cancellationToken)
    {
        var field = property.PersonalDataField!;
        if (property.Before is { } before)
        {
            var value = ReadValue(before);
            var subjectId = await ResolveSubjectAsync(field, value, actorId, cancellationToken);
            dbContext.Query<AuditPersonalDataEntity>().Add(
                new AuditPersonalDataEntity(eventId, subjectId, $"{field}.before", value));
        }

        if (property.After is { } after)
        {
            var value = ReadValue(after);
            var subjectId = await ResolveSubjectAsync(field, value, actorId, cancellationToken);
            dbContext.Query<AuditPersonalDataEntity>().Add(
                new AuditPersonalDataEntity(eventId, subjectId, $"{field}.after", value));
        }
    }

    private async Task<UserProfileId?> ResolveSubjectAsync(
        string field,
        string value,
        UserProfileId actorId,
        CancellationToken cancellationToken)
    {
        if (field == DisplayNameField)
        {
            return actorId;
        }

        var matchingProfileIds = await dbContext.Query<UserProfile>()
            .Where(profile => profile.Email != null
                && EF.Functions.ILike(profile.Email, EscapeLikePattern(value), LikeEscapeCharacter))
            .Select(profile => (UserProfileId?)profile.Id)
            .Take(2)
            .ToArrayAsync(cancellationToken);
        return matchingProfileIds.Length == 1 ? matchingProfileIds[0] : null;
    }

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private static string CreateDiff(IReadOnlyList<AuditPropertyChange> properties)
    {
        var diff = new JsonObject();
        foreach (var property in properties)
        {
            diff[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = new JsonObject
            {
                ["before"] = ToDiffNode(property.Before, property.PersonalDataField),
                ["after"] = ToDiffNode(property.After, property.PersonalDataField)
            };
        }

        return diff.ToJsonString();
    }

    private static JsonNode? ToDiffNode(JsonElement? value, string? personalDataField)
    {
        if (value is null)
        {
            return null;
        }

        if (personalDataField is not null)
        {
            return new JsonObject { ["$personal"] = personalDataField };
        }

        return JsonNode.Parse(value.Value.GetRawText());
    }

    private static string ReadValue(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? value.GetString() ?? string.Empty
        : value.GetRawText();
}
