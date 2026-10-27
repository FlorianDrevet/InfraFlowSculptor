using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Persistence.Auditing;

public sealed class EfAuditEventReader(IIfsDbContext dbContext) : IAuditEventReader
{
    public async Task<AuditEventPage> ListAsync(
        OrganizationId organizationId,
        DateTimeOffset? from,
        DateTimeOffset? through,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var eventsQuery = dbContext.Query<AuditEventEntity>()
            .AsNoTracking()
            .Where(auditEvent => auditEvent.OrganizationId == organizationId);
        if (from is { } start)
        {
            eventsQuery = eventsQuery.Where(auditEvent => auditEvent.OccurredAt >= start);
        }

        if (through is { } end)
        {
            eventsQuery = eventsQuery.Where(auditEvent => auditEvent.OccurredAt <= end);
        }

        var totalCount = await eventsQuery.LongCountAsync(cancellationToken);
        var events = await eventsQuery
            .OrderByDescending(auditEvent => auditEvent.OccurredAt)
            .ThenByDescending(auditEvent => auditEvent.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(auditEvent => new AuditEventData(
                auditEvent.Id,
                auditEvent.OccurredAt,
                auditEvent.ActorKind,
                auditEvent.ActorRef,
                auditEvent.Via,
                auditEvent.Action,
                auditEvent.ObjectType,
                auditEvent.ObjectId,
                auditEvent.Diff))
            .ToListAsync(cancellationToken);

        var profileIds = events
            .Where(auditEvent => auditEvent.ActorKind == AuditActorKinds.User
                && Guid.TryParse(auditEvent.ActorRef, out _))
            .Select(auditEvent => new UserProfileId(Guid.Parse(auditEvent.ActorRef)))
            .Distinct()
            .ToArray();
        var displayNames = await dbContext.Query<UserProfile>()
            .AsNoTracking()
            .Where(profile => profileIds.Contains(profile.Id))
            .Select(profile => new { profile.Id, profile.DisplayName })
            .ToDictionaryAsync(profile => profile.Id, profile => profile.DisplayName, cancellationToken);

        var eventIds = events.Select(auditEvent => auditEvent.Id).ToArray();
        var personalData = await dbContext.Query<AuditPersonalDataEntity>()
            .AsNoTracking()
            .Where(personalData => eventIds.Contains(personalData.EventId))
            .Select(personalData => new { personalData.EventId, personalData.Field, personalData.Value })
            .ToListAsync(cancellationToken);
        var personalValues = personalData.ToDictionary(
            personalData => (personalData.EventId, personalData.Field),
            personalData => personalData.Value);

        var items = events.Select(auditEvent => new AuditEventItem(
                auditEvent.Id,
                auditEvent.OccurredAt,
                ResolveActorName(auditEvent.ActorRef, auditEvent.ActorKind, displayNames),
                auditEvent.Via,
                auditEvent.Action,
                auditEvent.ObjectType,
                auditEvent.ObjectId,
                ReadDiff(auditEvent.Id, auditEvent.Diff, personalValues)))
            .ToArray();

        return new AuditEventPage(items, page, pageSize, totalCount);
    }

    private static AuditEventDiff ReadDiff(
        Guid eventId,
        string diffJson,
        Dictionary<(Guid EventId, string Field), string> personalValues)
    {
        using var document = JsonDocument.Parse(diffJson);
        var changes = document.RootElement.EnumerateObject()
            .Select(property =>
            {
                var before = ReadValue(eventId, "before", property.Value.GetProperty("before"), personalValues);
                var after = ReadValue(eventId, "after", property.Value.GetProperty("after"), personalValues);
                return new AuditPropertyDiff(property.Name, before.Value, after.Value, before.IsPersonal || after.IsPersonal);
            })
            .ToArray();

        return new AuditEventDiff(changes);
    }

    private static (string? Value, bool IsPersonal) ReadValue(
        Guid eventId,
        string side,
        JsonElement value,
        Dictionary<(Guid EventId, string Field), string> personalValues)
    {
        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("$personal", out var field)
            && field.ValueKind == JsonValueKind.String)
        {
            var key = (eventId, $"{field.GetString()}.{side}");
            return personalValues.TryGetValue(key, out var personalValue)
                ? (personalValue, true)
                : (null, true);
        }

        return (FormatValue(value), false);
    }

    private static string? FormatValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(FormatArrayValue)),
        JsonValueKind.Object => value.GetRawText(),
        _ => value.ToString()
    };

    private static string FormatArrayValue(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();

    private static string ResolveActorName(
        string actorRef,
        string actorKind,
        Dictionary<UserProfileId, string> displayNames)
    {
        if (actorKind == AuditActorKinds.User
            && Guid.TryParse(actorRef, out var actorId)
            && displayNames.TryGetValue(new UserProfileId(actorId), out var displayName))
        {
            return displayName;
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actorRef)))
            .ToLowerInvariant()[..8];
        return $"Utilisateur supprimé · {fingerprint}";
    }

    private sealed record AuditEventData(
        Guid Id,
        DateTimeOffset OccurredAt,
        string ActorKind,
        string ActorRef,
        AuditVia Via,
        string Action,
        ObjectType ObjectType,
        Guid ObjectId,
        string Diff);
}
