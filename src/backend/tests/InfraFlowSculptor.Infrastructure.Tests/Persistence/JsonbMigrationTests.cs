using InfraFlowSculptor.Application.Common.Security;
using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraFlowSculptor.Infrastructure.Tests.Persistence;

public sealed class JsonbMigrationTests(PostgreSqlFixture postgres) : IClassFixture<PostgreSqlFixture>
{
    private const string PreviousMigration = "20261028095208_J0_10_ComponentsAndResourceGroups";

    private sealed class FixedOrganization(OrganizationId? id) : ICurrentOrganization
    {
        public OrganizationId? Id { get; } = id;
    }

    [Fact]
    public async Task ExistingJsonbValuesSurviveMigrationAndNoJsonbColumnsRemain()
    {
        await using var dbContext = postgres.CreatePersistenceDbContext(new FixedOrganization(null));
        await dbContext.Database.MigrateAsync(PreviousMigration);

        var organizationId = Guid.NewGuid();
        var userProfileId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userObjectId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        var userIdempotencyKey = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var outboxId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var singleComponentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var responseJson = """{"StatusCode":201,"ContentType":"application/json","Headers":{"Location":["/v1/projects/1"]},"Body":"eyJpZCI6MX0="}""";
        const string draftJson = """{"step":4,"name":"Migration test","environments":[{"code":"dev"}]}""";
        const string diffJson = """{"name":{"before":"Old","after":"New"}}""";
        const string payloadJson = """{"JobId":"00000000-0000-0000-0000-000000000001","Type":"test","Payload":{"value":7}}""";

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO organizations (id, name, version) VALUES ({organizationId}, 'Migration test', 1)
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_profiles (id, tenant_id, object_id, display_name, preferred_language, preferred_theme, created_at, last_seen_at)
            VALUES ({userProfileId}, {tenantId}, {userObjectId}, 'Test user', 'fr', 'dark', {now}, {now})
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO projects (id, organization_id, name, code, version, deleted_at)
            VALUES ({projectId}, {organizationId}, 'Single target migration', 'singlemig', 1, NULL)
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO components (
                id, project_id, organization_id, name, code, name_key, code_key, description, mode,
                targets_all_environments, target_environment_ids, removed_resource_behavior,
                deletion_protection_enabled, version, first_published_at)
            VALUES (
                {singleComponentId}, {projectId}, {organizationId}, 'Single component', 'single-component',
                'SINGLE COMPONENT', 'SINGLE-COMPONENT', '', 'Single', false, ARRAY[]::uuid[], 'Detach', true, 1, NULL)
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO component_single_targets (
                component_id, code, subscription_id, region, deployment_connection, is_protected, approvers, runners)
            VALUES (
                {singleComponentId}, 'main', {Guid.NewGuid()}, 'francecentral', 'connection', false,
                ARRAY[]::text[], ARRAY[]::text[])
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO project_wizard_drafts (organization_id, user_profile_id, content_json, expires_at)
            VALUES ({organizationId}, {userProfileId}, CAST({draftJson} AS jsonb), {now.AddDays(30)})
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO audit_events (id, organization_id, occurred_at, actor_kind, actor_ref, via, action, object_type, object_id, diff)
            VALUES ({eventId}, {organizationId}, {now}, 'user', 'test-user', 'Screen', 'project.updated', 'Project', {Guid.NewGuid()}, CAST({diffJson} AS jsonb))
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO idempotency_keys (organization_id, key, request_hash, status_code, response, created_at)
            VALUES ({organizationId}, {idempotencyKey}, repeat('A', 64), 201, CAST({responseJson} AS jsonb), {now})
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_idempotency_keys (tenant_id, user_object_id, key, request_hash, status_code, response, created_at)
            VALUES ({tenantId}, {userObjectId}, {userIdempotencyKey}, repeat('B', 64), 201, CAST({responseJson} AS jsonb), {now})
            """);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO outbox_messages (id, organization_id, queue, session_id, type, payload, created_at, attempts)
            VALUES ({outboxId}, {organizationId}, 'notifications', {organizationId.ToString("D")}, 'test-job', CAST({payloadJson} AS jsonb), {now}, 0)
            """);

        var oldDraft = await dbContext.Database.SqlQuery<string>($"""
            SELECT content_json::text AS "Value"
            FROM project_wizard_drafts
            WHERE organization_id = {organizationId} AND user_profile_id = {userProfileId}
            """).SingleAsync();
        var oldDiff = await dbContext.Database.SqlQuery<string>($"""
            SELECT diff::text AS "Value" FROM audit_events WHERE id = {eventId}
            """).SingleAsync();
        var oldOrganizationResponse = await dbContext.Database.SqlQuery<string?>($"""
            SELECT response::text AS "Value" FROM idempotency_keys WHERE organization_id = {organizationId} AND key = {idempotencyKey}
            """).SingleAsync();
        var oldUserResponse = await dbContext.Database.SqlQuery<string?>($"""
            SELECT response::text AS "Value" FROM user_idempotency_keys WHERE tenant_id = {tenantId} AND user_object_id = {userObjectId} AND key = {userIdempotencyKey}
            """).SingleAsync();
        var oldPayload = await dbContext.Database.SqlQuery<string>($"""
            SELECT payload::text AS "Value" FROM outbox_messages WHERE id = {outboxId}
            """).SingleAsync();

        await dbContext.Database.MigrateAsync();

        var newDraft = await dbContext.Database.SqlQuery<string>($"""
            SELECT content_json AS "Value"
            FROM project_wizard_drafts
            WHERE organization_id = {organizationId} AND user_profile_id = {userProfileId}
            """).SingleAsync();
        var newDiff = await dbContext.Database.SqlQuery<string>($"""
            SELECT diff AS "Value" FROM audit_events WHERE id = {eventId}
            """).SingleAsync();
        var newOrganizationResponse = await dbContext.Database.SqlQuery<string?>($"""
            SELECT response AS "Value" FROM idempotency_keys WHERE organization_id = {organizationId} AND key = {idempotencyKey}
            """).SingleAsync();
        var newUserResponse = await dbContext.Database.SqlQuery<string?>($"""
            SELECT response AS "Value" FROM user_idempotency_keys WHERE tenant_id = {tenantId} AND user_object_id = {userObjectId} AND key = {userIdempotencyKey}
            """).SingleAsync();
        var newPayload = await dbContext.Database.SqlQuery<string>($"""
            SELECT payload AS "Value" FROM outbox_messages WHERE id = {outboxId}
            """).SingleAsync();
        var remainingJsonbColumns = await dbContext.Database.SqlQuery<string>($"""
            SELECT table_name || '.' || column_name AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'public' AND udt_name = 'jsonb'
            """).ToArrayAsync();
        var migratedSingleTargetId = await dbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value"
            FROM component_single_targets
            WHERE component_id = {singleComponentId}
            """).SingleAsync();

        Assert.Equal(oldDraft, newDraft);
        Assert.Equal(oldDiff, newDiff);
        Assert.Equal(oldOrganizationResponse, newOrganizationResponse);
        Assert.Equal(oldUserResponse, newUserResponse);
        Assert.Equal(oldPayload, newPayload);
        Assert.Empty(remainingJsonbColumns);
        Assert.NotEqual(Guid.Empty, migratedSingleTargetId);
    }

}
