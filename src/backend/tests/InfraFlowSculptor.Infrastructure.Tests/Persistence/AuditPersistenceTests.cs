using System.Security.Cryptography;
using System.Text;
using InfraFlowSculptor.Application.Common.Auditing;
using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using InfraFlowSculptor.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class AuditPersistenceTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task AuditEventsRejectDirectMutationAndOnlyPurgeRecordsOlderThanRetention()
    {
        var clock = TimeProvider.System;
        var organizationId = new OrganizationId(Guid.CreateVersion7(clock.GetUtcNow()));
        var auditEventId = Guid.CreateVersion7(clock.GetUtcNow());
        var recentAuditEventId = Guid.CreateVersion7(clock.GetUtcNow().AddTicks(1));
        await using var dbContext = postgres.CreatePersistenceDbContext(new FixedOrganization(organizationId));
        await dbContext.Database.MigrateAsync();

        var organization = OrganizationAggregate.Create(
            organizationId,
            "Contoso Audit",
            new UserProfileId(Guid.CreateVersion7(clock.GetUtcNow())));
        Assert.False(organization.IsError);
        dbContext.Organizations.Add(organization.Value);
        dbContext.AuditEvents.Add(new AuditEventEntity(
            auditEventId,
            organizationId,
            clock.GetUtcNow().AddMonths(-15),
            "user",
            Guid.NewGuid().ToString("D"),
            AuditVia.Screen,
            "organization.renamed",
            ObjectType.Organization,
            organizationId.Value,
            "{\"name\":{\"before\":\"Old\",\"after\":\"New\"}}"));
        dbContext.AuditEvents.Add(new AuditEventEntity(
            recentAuditEventId,
            organizationId,
            clock.GetUtcNow().AddMonths(-12),
            "user",
            Guid.NewGuid().ToString("D"),
            AuditVia.Screen,
            "organization.renamed",
            ObjectType.Organization,
            organizationId.Value,
            "{\"name\":{\"before\":\"Earlier\",\"after\":\"Recent\"}}"));
        await dbContext.SaveChangesAsync();

        var updateException = await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE audit_events SET action = 'tampered' WHERE id = {auditEventId}"));
        Assert.Equal("55000", updateException.SqlState);

        var deleteException = await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM audit_events WHERE id = {auditEventId}"));
        Assert.Equal("55000", deleteException.SqlState);

        var purged = await dbContext.Database.SqlQuery<long>(
                $"SELECT public.purge_audit_events({clock.GetUtcNow().AddYears(100)}) AS \"Value\"")
            .SingleAsync();

        Assert.Equal(1, purged);
        Assert.False(await dbContext.AuditEvents.IgnoreQueryFilters()
            .AnyAsync(auditEvent => auditEvent.Id == auditEventId));
        Assert.True(await dbContext.AuditEvents.IgnoreQueryFilters()
            .AnyAsync(auditEvent => auditEvent.Id == recentAuditEventId));
    }

    [Fact]
    public async Task DeletingASubjectProfileRemovesPersonalAuditValuesAndKeepsAPseudonymizedEvent()
    {
        const string personalEmail = "private-subject@contoso.example";
        var now = TimeProvider.System.GetUtcNow();
        var organizationId = new OrganizationId(Guid.CreateVersion7(now));
        var owner = UserProfile.Create(
            new UserProfileId(Guid.CreateVersion7(now.AddTicks(1))),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Organization Owner",
            "owner@contoso.example",
            now).Value;
        var subject = UserProfile.Create(
            new UserProfileId(Guid.CreateVersion7(now.AddTicks(2))),
            new UserKey(Guid.NewGuid(), Guid.NewGuid()),
            "Private Subject",
            personalEmail,
            now).Value;
        var auditEventId = Guid.CreateVersion7(now.AddTicks(3));
        await using var dbContext = postgres.CreatePersistenceDbContext(new FixedOrganization(organizationId));
        await dbContext.Database.MigrateAsync();

        dbContext.UserProfiles.AddRange(owner, subject);
        var organization = OrganizationAggregate.Create(organizationId, "Contoso Privacy", owner.Id);
        Assert.False(organization.IsError);
        dbContext.Organizations.Add(organization.Value);
        dbContext.AuditEvents.Add(new AuditEventEntity(
            auditEventId,
            organizationId,
            now,
            "user",
            subject.Id.Value.ToString("D"),
            AuditVia.Screen,
            "organization.invitation.sent",
            ObjectType.Invitation,
            Guid.NewGuid(),
            "{\"email\":{\"before\":null,\"after\":{\"$personal\":\"email\"}}}"));
        dbContext.AuditPersonalData.Add(new AuditPersonalDataEntity(
            auditEventId,
            subject.Id,
            "email.after",
            personalEmail));
        await dbContext.SaveChangesAsync();

        dbContext.UserProfiles.Remove(subject);
        await dbContext.SaveChangesAsync();

        Assert.False(await dbContext.AuditPersonalData.IgnoreQueryFilters()
            .AnyAsync(personalData => personalData.SubjectUserId == subject.Id));
        var retainedEvent = await dbContext.AuditEvents.IgnoreQueryFilters()
            .SingleAsync(auditEvent => auditEvent.Id == auditEventId);
        Assert.DoesNotContain(personalEmail, retainedEvent.Diff, StringComparison.Ordinal);

        var page = await new EfAuditEventReader(dbContext).ListAsync(
            organizationId,
            null,
            null,
            page: 1,
            pageSize: 50,
            CancellationToken.None);
        var item = Assert.Single(page.Items);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subject.Id.Value.ToString("D"))))
            .ToLowerInvariant()[..8];
        Assert.Equal($"Utilisateur supprimé · {fingerprint}", item.ActorDisplayName);
        var change = Assert.Single(item.Diff.Changes);
        Assert.True(change.IsPersonalData);
        Assert.Null(change.After);
    }

    private sealed class FixedOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }
}
