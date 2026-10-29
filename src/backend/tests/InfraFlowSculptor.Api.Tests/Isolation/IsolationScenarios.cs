using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using InfraFlowSculptor.Api.Configuration;
using InfraFlowSculptor.Api.Tests.Common;

namespace InfraFlowSculptor.Api.Tests.Isolation;

public sealed record IsolationScenario(
    string RouteName,
    Func<string, HttpRequestMessage> CreateRequest,
    HttpStatusCode ExpectedStatusCode);

public static class IsolationScenarios
{
    private const string DavidTenantId = "22222222-2222-2222-2222-222222222222";
    private const string DavidObjectId = "dddddddd-dddd-4ddd-8ddd-dddddddddddd";
    private const string ContosoOrganizationId = "33333333-3333-4333-8333-333333333333";
    private const string TestJobId = "55555555-5555-4555-8555-555555555555";
    private const string TestMemberId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string TestInvitationId = "66666666-6666-4666-8666-666666666666";
    private const string TestComponentId = "77777777-7777-4777-8777-777777777777";
    private const string TestResourceGroupId = "88888888-8888-4888-8888-888888888888";
    private const string TestResourceId = "99999999-9999-4999-8999-999999999999";
    private const string TestResourceChildId = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
    private const string TestInvitationToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static readonly string ShopProjectId = DemoDataSeeder.ShopProjectId.Value.ToString("D");
    private static readonly string[] MemberRoles = ["Member"];
    private static readonly string[] AllowedEmailDomains = ["contoso.example"];
    private static readonly string[] AllowedTenantIds = ["11111111-1111-4111-8111-111111111111"];

    public static IReadOnlyDictionary<string, IsolationScenario> All { get; } =
        new Dictionary<string, IsolationScenario>(StringComparer.Ordinal)
        {
            ["GetVersion"] = Scenario("GetVersion", HttpMethod.Get, "/v1/version", HttpStatusCode.OK),
            ["GetMe"] = AuthenticatedScenario("GetMe", HttpMethod.Get, "/v1/me", HttpStatusCode.OK),
            ["UpdateMePreferences"] = AuthenticatedScenario(
                "UpdateMePreferences",
                HttpMethod.Put,
                "/v1/me/preferences",
                HttpStatusCode.OK,
                new { preferredLanguage = "fr", preferredTheme = "system" }),
            ["ListMyOrganizations"] = AuthenticatedScenario(
                "ListMyOrganizations",
                HttpMethod.Get,
                "/v1/organizations/",
                HttpStatusCode.OK),
            ["CreateOrganization"] = AuthenticatedScenario(
                "CreateOrganization",
                HttpMethod.Post,
                "/v1/organizations/",
                HttpStatusCode.BadRequest,
                new { name = "A" }),
            ["GetOrganization"] = OrganizationScenario(
                "GetOrganization",
                HttpMethod.Get,
                $"/v1/organizations/{ContosoOrganizationId}",
                HttpStatusCode.NotFound),
            ["RenameOrganization"] = OrganizationScenario(
                "RenameOrganization",
                HttpMethod.Put,
                $"/v1/organizations/{ContosoOrganizationId}",
                HttpStatusCode.NotFound,
                new { name = "Forbidden Rename" }),
            ["ListOrganizationMembers"] = OrganizationScenario(
                "ListOrganizationMembers",
                HttpMethod.Get,
                $"/v1/organizations/{ContosoOrganizationId}/members",
                HttpStatusCode.NotFound),
            ["ListOrganizationAuditEvents"] = OrganizationScenario(
                "ListOrganizationAuditEvents",
                HttpMethod.Get,
                $"/v1/organizations/{ContosoOrganizationId}/audit",
                HttpStatusCode.NotFound),
            ["ChangeOrganizationMemberRoles"] = OrganizationScenario(
                "ChangeOrganizationMemberRoles",
                HttpMethod.Put,
                $"/v1/organizations/{ContosoOrganizationId}/members/{TestMemberId}/roles",
                HttpStatusCode.NotFound,
                new { roles = MemberRoles }),
            ["RemoveOrganizationMember"] = OrganizationScenario(
                "RemoveOrganizationMember",
                HttpMethod.Delete,
                $"/v1/organizations/{ContosoOrganizationId}/members/{TestMemberId}",
                HttpStatusCode.NotFound),
            ["LeaveOrganization"] = OrganizationScenario(
                "LeaveOrganization",
                HttpMethod.Post,
                $"/v1/organizations/{ContosoOrganizationId}/leave",
                HttpStatusCode.NotFound),
            ["UpdateOrganizationAccessRestrictions"] = OrganizationScenario(
                "UpdateOrganizationAccessRestrictions",
                HttpMethod.Put,
                $"/v1/organizations/{ContosoOrganizationId}/access-restrictions",
                HttpStatusCode.NotFound,
                new { allowedEmailDomains = AllowedEmailDomains, allowedTenantIds = AllowedTenantIds }),
            ["ListOrganizationInvitations"] = OrganizationScenario(
                "ListOrganizationInvitations",
                HttpMethod.Get,
                $"/v1/organizations/{ContosoOrganizationId}/invitations",
                HttpStatusCode.NotFound),
            ["InviteOrganizationMember"] = OrganizationScenario(
                "InviteOrganizationMember",
                HttpMethod.Post,
                $"/v1/organizations/{ContosoOrganizationId}/invitations",
                HttpStatusCode.NotFound,
                new { email = "new@contoso.example", roles = MemberRoles }),
            ["ResendOrganizationInvitation"] = OrganizationScenario(
                "ResendOrganizationInvitation",
                HttpMethod.Post,
                $"/v1/organizations/{ContosoOrganizationId}/invitations/{TestInvitationId}/resend",
                HttpStatusCode.NotFound),
            ["RevokeOrganizationInvitation"] = OrganizationScenario(
                "RevokeOrganizationInvitation",
                HttpMethod.Delete,
                $"/v1/organizations/{ContosoOrganizationId}/invitations/{TestInvitationId}",
                HttpStatusCode.NotFound),
            ["AcceptInvitation"] = AuthenticatedScenario(
                "AcceptInvitation",
                HttpMethod.Post,
                $"/v1/invitations/{TestInvitationToken}/accept",
                HttpStatusCode.NotFound),
            ["GetInvitationPreview"] = AuthenticatedScenario(
                "GetInvitationPreview",
                HttpMethod.Get,
                $"/v1/invitations/{TestInvitationToken}",
                HttpStatusCode.NotFound),
            ["GetFeatures"] = AuthenticatedScenario("GetFeatures", HttpMethod.Get, "/v1/features", HttpStatusCode.OK),
            ["GetCatalogVersions"] = AuthenticatedScenario(
                "GetCatalogVersions",
                HttpMethod.Get,
                "/v1/catalog/versions",
                HttpStatusCode.OK),
            ["GetCatalogTypes"] = AuthenticatedScenario(
                "GetCatalogTypes",
                HttpMethod.Get,
                "/v1/catalog/2026.10-pilot/types?category=security",
                HttpStatusCode.OK),
            ["GetCatalogType"] = AuthenticatedScenario(
                "GetCatalogType",
                HttpMethod.Get,
                "/v1/catalog/2026.10-pilot/types/KeyVault",
                HttpStatusCode.OK),
            ["GetCatalogRegions"] = AuthenticatedScenario(
                "GetCatalogRegions",
                HttpMethod.Get,
                "/v1/catalog/2026.10-pilot/regions",
                HttpStatusCode.OK),
            ["GetMyProjectPermissions"] = OrganizationScenario(
                "GetMyProjectPermissions",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/permissions/me",
                HttpStatusCode.NotFound),
            ["GetProjectMemberPermissions"] = OrganizationScenario(
                "GetProjectMemberPermissions",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/members/{TestMemberId}/permissions",
                HttpStatusCode.NotFound),
            ["AssignProjectRole"] = OrganizationScenario(
                "AssignProjectRole",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/members/{TestMemberId}/roles",
                HttpStatusCode.NotFound,
                new { role = "Reader", scope = "Project", expectedVersion = 1 }),
            ["RemoveProjectRole"] = OrganizationScenario(
                "RemoveProjectRole",
                HttpMethod.Delete,
                $"/v1/projects/{ShopProjectId}/members/{TestMemberId}/roles/Reader?scope=Project&expectedVersion=1",
                HttpStatusCode.NotFound),
            ["TakeProjectOwnership"] = OrganizationScenario(
                "TakeProjectOwnership",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/ownership",
                HttpStatusCode.NotFound,
                new { expectedVersion = 1 }),
            ["ListProjects"] = OrganizationScenario(
                "ListProjects",
                HttpMethod.Get,
                "/v1/projects?includeDeleted=true",
                HttpStatusCode.NotFound),
            ["CreateProject"] = OrganizationScenario(
                "CreateProject",
                HttpMethod.Post,
                "/v1/projects",
                HttpStatusCode.NotFound,
                new { name = "Isolated Project", code = "isolated", tenantId = Guid.NewGuid(), description = "" }),
            ["GetWizardDraft"] = OrganizationScenario(
                "GetWizardDraft",
                HttpMethod.Get,
                "/v1/projects/wizard-draft",
                HttpStatusCode.NotFound),
            ["SaveWizardDraft"] = OrganizationScenario(
                "SaveWizardDraft",
                HttpMethod.Put,
                "/v1/projects/wizard-draft",
                HttpStatusCode.NotFound,
                new { content = new { step = 1 } }),
            ["DiscardWizardDraft"] = OrganizationScenario(
                "DiscardWizardDraft",
                HttpMethod.Delete,
                "/v1/projects/wizard-draft",
                HttpStatusCode.NotFound),
            ["CreateProjectFromWizard"] = new IsolationScenario(
                "CreateProjectFromWizard",
                _ =>
                {
                    var request = CreateRequest(
                        HttpMethod.Post,
                        "/v1/projects/from-wizard",
                        CreateDavidToken(),
                        new
                        {
                            name = "Isolated Wizard Project",
                            code = "isolated",
                            tenantId = Guid.NewGuid(),
                            description = "",
                            namingPreset = "Compact",
                            defaultNameTemplate = "{abbr}-{project}-{name}-{env}",
                            resourceGroupNameTemplate = "rg-{project}-{component}-{name}-{env}",
                            environments = new[] { new { name = "Development", code = "dev", subscriptionId = Guid.NewGuid(), region = "francecentral", isProtected = false, approvers = Array.Empty<string>(), description = "" } },
                            tags = new Dictionary<string, string>(),
                            publishingPreset = (string?)null
                        });
                    request.Headers.Add("X-Ifs-Organization", ContosoOrganizationId);
                    request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
                    return request;
                },
                HttpStatusCode.NotFound),
            ["PreviewNaming"] = OrganizationScenario(
                "PreviewNaming",
                HttpMethod.Post,
                "/v1/naming/preview",
                HttpStatusCode.NotFound,
                new
                {
                    projectName = "Isolated",
                    projectCode = "isolated",
                    namingPreset = "Compact",
                    defaultNameTemplate = "{abbr}-{project}-{name}-{env}",
                    resourceGroupNameTemplate = "rg-{project}-{component}-{name}-{env}",
                    environments = new[] { new { code = "dev", region = "francecentral" } },
                    tags = new Dictionary<string, string>()
                }),
            ["GetProjectNaming"] = OrganizationScenario(
                "GetProjectNaming",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/naming",
                HttpStatusCode.NotFound),
            ["SetProjectNaming"] = OrganizationWriteScenario(
                "SetProjectNaming",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/naming",
                HttpStatusCode.NotFound,
                new
                {
                    preset = "Compact",
                    defaultTemplate = "{abbr}-{project}-{name}-{env}",
                    resourceGroupTemplate = "rg-{project}-{component}-{name}-{env}",
                    typeOverrides = Array.Empty<object>(),
                    expectedVersion = 1
                }),
            ["PreviewProjectNaming"] = OrganizationScenario(
                "PreviewProjectNaming",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/naming/preview",
                HttpStatusCode.NotFound,
                new
                {
                    preset = "Compact",
                    defaultTemplate = "{abbr}-{project}-{name}-{env}",
                    resourceGroupTemplate = "rg-{project}-{component}-{name}-{env}",
                    typeOverrides = Array.Empty<object>()
                }),
            ["GetProject"] = OrganizationScenario(
                "GetProject",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}",
                HttpStatusCode.NotFound),
            ["ListProjectComponents"] = OrganizationScenario(
                "ListProjectComponents",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/components",
                HttpStatusCode.NotFound),
            ["CreateProjectComponent"] = OrganizationWriteScenario(
                "CreateProjectComponent",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/components",
                HttpStatusCode.NotFound,
                new
                {
                    name = "Isolated component",
                    code = "isolated",
                    description = "",
                    mode = "PerEnvironment",
                    targetsAllEnvironments = true,
                    targetEnvironmentIds = Array.Empty<Guid>(),
                    singleTarget = (object?)null,
                    removedResourceBehavior = "Detach",
                    deletionProtectionEnabled = true,
                    tags = new Dictionary<string, string>(),
                    expectedVersion = 1
                }),
            ["GetProjectComponent"] = OrganizationScenario(
                "GetProjectComponent",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}",
                HttpStatusCode.NotFound),
            ["SetComponentDefaultLogAnalytics"] = OrganizationWriteScenario(
                "SetComponentDefaultLogAnalytics",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/default-log-analytics",
                HttpStatusCode.NotFound,
                new { workspaceResourceId = (Guid?)null, expectedVersion = 1 }),
            ["GetEffectiveValues"] = OrganizationScenario(
                "GetEffectiveValues",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/resources/{TestResourceId}/effective-values",
                HttpStatusCode.NotFound),
            ["GetResourceDeletionImpact"] = OrganizationScenario(
                "GetResourceDeletionImpact",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/delete-impact",
                HttpStatusCode.NotFound),
            ["GetResourceLinks"] = OrganizationScenario(
                "GetResourceLinks",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/links",
                HttpStatusCode.NotFound),
            ["GetResourceLinkOptions"] = OrganizationScenario(
                "GetResourceLinkOptions",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/links/options",
                HttpStatusCode.NotFound),
            ["PreviewLink"] = OrganizationScenario(
                "PreviewLink",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/links/preview",
                HttpStatusCode.NotFound,
                new
                {
                    targetComponentId = TestComponentId,
                    targetResourceId = TestResourceId,
                    kind = "Access",
                    identityKind = "System",
                    identityResourceId = (Guid?)null,
                    roles = Array.Empty<string>(),
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null
                }),
            ["GetAppSettings"] = OrganizationScenario(
                "GetAppSettings",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/app-settings",
                HttpStatusCode.NotFound),
            ["GetResourceApplication"] = OrganizationScenario(
                "GetResourceApplication",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/application",
                HttpStatusCode.NotFound),
            ["CreateAppSetting"] = OrganizationWriteScenario(
                "CreateAppSetting",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/app-settings",
                HttpStatusCode.NotFound,
                new
                {
                    name = "ISOLATION_SETTING",
                    sourceKind = "Literal",
                    literalValue = "isolated",
                    overrides = new Dictionary<Guid, string?>(),
                    sourceResourceId = (Guid?)null,
                    outputName = (string?)null,
                    keyVaultResourceId = (Guid?)null,
                    secretName = (string?)null,
                    secretFeedKind = (string?)null,
                    pipelineVariableName = (string?)null,
                    identityResourceId = (Guid?)null,
                    forceNonSecret = false
                }),
            ["UpdateAppSetting"] = OrganizationWriteScenario(
                "UpdateAppSetting",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/app-settings/{TestResourceChildId}",
                HttpStatusCode.NotFound,
                new
                {
                    name = "ISOLATION_SETTING",
                    sourceKind = "Literal",
                    literalValue = "isolated",
                    overrides = new Dictionary<Guid, string?>(),
                    sourceResourceId = (Guid?)null,
                    outputName = (string?)null,
                    keyVaultResourceId = (Guid?)null,
                    secretName = (string?)null,
                    secretFeedKind = (string?)null,
                    pipelineVariableName = (string?)null,
                    identityResourceId = (Guid?)null,
                    forceNonSecret = false,
                    expectedVersion = 1
                }),
            ["DeleteAppSetting"] = OrganizationWriteScenario(
                "DeleteAppSetting",
                HttpMethod.Delete,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/app-settings/{TestResourceChildId}",
                HttpStatusCode.NotFound,
                new { expectedVersion = 1 }),
            ["GetLinkRemovalImpact"] = OrganizationScenario(
                "GetLinkRemovalImpact",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/links/{TestResourceChildId}/impact",
                HttpStatusCode.NotFound),
            ["AddLink"] = OrganizationWriteScenario(
                "AddLink",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/links",
                HttpStatusCode.NotFound,
                new
                {
                    targetComponentId = TestComponentId,
                    targetResourceId = TestResourceId,
                    kind = "Access",
                    identityKind = "System",
                    identityResourceId = (Guid?)null,
                    roles = new[] { "Reader" },
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedComponentVersion = 1
                }),
            ["UpdateLink"] = OrganizationWriteScenario(
                "UpdateLink",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/links/{TestResourceChildId}",
                HttpStatusCode.NotFound,
                new
                {
                    targetComponentId = TestComponentId,
                    targetResourceId = TestResourceId,
                    kind = "Access",
                    identityKind = "System",
                    identityResourceId = (Guid?)null,
                    roles = new[] { "Reader" },
                    accessScope = "Resource",
                    dataAccessLevel = (string?)null,
                    expectedLinkVersion = 1
                }),
            ["RemoveLink"] = OrganizationWriteScenario(
                "RemoveLink",
                HttpMethod.Delete,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/links/{TestResourceChildId}",
                HttpStatusCode.NotFound,
                new { expectedLinkVersion = 1 }),
            ["PreviewResourceName"] = OrganizationScenario(
                "PreviewResourceName",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/preview-name",
                HttpStatusCode.NotFound,
                new { resourceGroupId = Guid.Parse(TestResourceGroupId), type = "KeyVault", logicalName = "isolated" }),
            ["UpdateProjectComponent"] = OrganizationWriteScenario(
                "UpdateProjectComponent",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}",
                HttpStatusCode.NotFound,
                new
                {
                    name = "Isolated component",
                    code = "isolated",
                    description = "",
                    mode = "PerEnvironment",
                    targetsAllEnvironments = true,
                    targetEnvironmentIds = Array.Empty<Guid>(),
                    singleTarget = (object?)null,
                    removedResourceBehavior = "Detach",
                    deletionProtectionEnabled = true,
                    tags = new Dictionary<string, string>(),
                    expectedVersion = 1
                }),
            ["SetComponentNaming"] = OrganizationWriteScenario(
                "SetComponentNaming",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/naming",
                HttpStatusCode.NotFound,
                new
                {
                    defaultTemplate = (string?)null,
                    typeOverrides = Array.Empty<object>(),
                    expectedVersion = 1
                }),
            ["DeleteProjectComponent"] = OrganizationWriteScenario(
                "DeleteProjectComponent",
                HttpMethod.Delete,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}",
                HttpStatusCode.NotFound,
                new { expectedVersion = 1 }),
            ["CreateResourceGroup"] = OrganizationWriteScenario(
                "CreateResourceGroup",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resource-groups",
                HttpStatusCode.NotFound,
                new
                {
                    name = "main",
                    region = (string?)null,
                    tags = new Dictionary<string, string>(),
                    forcedNames = new Dictionary<Guid, string>(),
                    expectedVersion = 1
                }),
            ["UpdateResourceGroup"] = OrganizationWriteScenario(
                "UpdateResourceGroup",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resource-groups/{TestResourceGroupId}",
                HttpStatusCode.NotFound,
                new
                {
                    name = "main",
                    region = (string?)null,
                    tags = new Dictionary<string, string>(),
                    forcedNames = new Dictionary<Guid, string>(),
                    expectedVersion = 1
                }),
            ["DeleteResourceGroup"] = OrganizationWriteScenario(
                "DeleteResourceGroup",
                HttpMethod.Delete,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resource-groups/{TestResourceGroupId}",
                HttpStatusCode.NotFound,
                new { expectedVersion = 1 }),
            ["AddResource"] = OrganizationWriteScenario(
                "AddResource",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources",
                HttpStatusCode.NotFound,
                new
                {
                    resourceGroupId = TestResourceGroupId,
                    type = "KeyVault",
                    logicalName = "isolated",
                    description = "",
                    isExisting = false,
                    properties = new { },
                    tags = new Dictionary<string, string>(),
                    expectedVersion = 1
                }),
            ["SetResourceProperties"] = OrganizationWriteScenario(
                "SetResourceProperties",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/properties",
                HttpStatusCode.NotFound,
                new { properties = new { }, expectedVersion = 1 }),
            ["SaveResourceApplication"] = OrganizationWriteScenario(
                "SaveResourceApplication",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/application",
                HttpStatusCode.NotFound,
                new
                {
                    expectedVersion = 0,
                    name = "isolated",
                    codeSourcePath = "src/isolated",
                    dockerfilePath = "src/isolated/Dockerfile",
                    buildContextPath = "src/isolated",
                    imageRepository = "shop/orders/isolated",
                    healthCheckPath = (string?)null,
                    deploymentStrategy = "Direct",
                    buildRegistryEnvironmentId = (Guid?)null,
                    steps = Array.Empty<object>()
                }),
            ["SetResourceDefaultIdentity"] = OrganizationWriteScenario(
                "SetResourceDefaultIdentity",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/default-identity",
                HttpStatusCode.NotFound,
                new { identityResourceId = (Guid?)null, expectedVersion = 1 }),
            ["UpdateResourceMetadata"] = OrganizationWriteScenario(
                "UpdateResourceMetadata",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/metadata",
                HttpStatusCode.NotFound,
                new { description = "isolated", tags = new Dictionary<string, string>(), expectedVersion = 1 }),
            ["SetOverride"] = OrganizationWriteScenario(
                "SetOverride",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/overrides",
                HttpStatusCode.NotFound,
                new { targetId = Guid.NewGuid(), property = "sku", value = "standard", expectedVersion = 1 }),
            ["ClearOverride"] = OrganizationWriteScenario(
                "ClearOverride",
                HttpMethod.Delete,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/overrides",
                HttpStatusCode.NotFound,
                new { targetId = Guid.NewGuid(), property = "sku", expectedVersion = 1 }),
            ["SetPresence"] = OrganizationWriteScenario(
                "SetPresence",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/presence",
                HttpStatusCode.NotFound,
                new { targetId = Guid.NewGuid(), isPresent = true, expectedVersion = 1 }),
            ["SetExistingResourceId"] = OrganizationWriteScenario(
                "SetExistingResourceId",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/existing-id",
                HttpStatusCode.NotFound,
                new { targetId = Guid.NewGuid(), resourceId = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/test", expectedVersion = 1 }),
            ["SetForcedName"] = OrganizationWriteScenario(
                "SetForcedName",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/forced-name",
                HttpStatusCode.NotFound,
                new { targetId = Guid.NewGuid(), name = "kv-isolated", expectedVersion = 1 }),
            ["MoveResource"] = OrganizationWriteScenario(
                "MoveResource",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/resource-group",
                HttpStatusCode.NotFound,
                new { resourceGroupId = TestResourceGroupId, expectedVersion = 1 }),
            ["DeleteResource"] = OrganizationWriteScenario(
                "DeleteResource",
                HttpMethod.Delete,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}",
                HttpStatusCode.NotFound,
                new { expectedVersion = 1 }),
            ["AddChild"] = OrganizationWriteScenario(
                "AddChild",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/children",
                HttpStatusCode.NotFound,
                new { type = "Container", name = "isolated", properties = new { }, expectedVersion = 1 }),
            ["UpdateChild"] = OrganizationWriteScenario(
                "UpdateChild",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/children/{TestResourceChildId}",
                HttpStatusCode.NotFound,
                new { type = "Container", name = "isolated", properties = new { }, expectedVersion = 1 }),
            ["RemoveChild"] = OrganizationWriteScenario(
                "RemoveChild",
                HttpMethod.Delete,
                $"/v1/projects/{ShopProjectId}/components/{TestComponentId}/resources/{TestResourceId}/children/{TestResourceChildId}",
                HttpStatusCode.NotFound,
                new { expectedVersion = 1 }),
            ["SetProjectFavorite"] = OrganizationWriteScenario(
                "SetProjectFavorite",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/favorite",
                HttpStatusCode.NotFound),
            ["RemoveProjectFavorite"] = OrganizationWriteScenario(
                "RemoveProjectFavorite",
                HttpMethod.Delete,
                $"/v1/projects/{ShopProjectId}/favorite",
                HttpStatusCode.NotFound),
            ["UpdateProjectSettings"] = OrganizationScenario(
                "UpdateProjectSettings",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}",
                HttpStatusCode.NotFound,
                new
                {
                    name = "Isolated Project",
                    code = "isolated",
                    description = "",
                    systemTagsEnabled = true,
                    coordinatorUserProfileId = (Guid?)null,
                    tags = new Dictionary<string, string>(),
                    confirmImpact = false,
                    expectedVersion = 1
                }),
            ["SetProjectTags"] = OrganizationScenario(
                "SetProjectTags",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/tags",
                HttpStatusCode.NotFound,
                new { tags = new Dictionary<string, string>(), expectedVersion = 1 }),
            ["DeleteProject"] = OrganizationScenario(
                "DeleteProject",
                HttpMethod.Delete,
                $"/v1/projects/{ShopProjectId}",
                HttpStatusCode.NotFound,
                new { nameConfirmation = "shop", expectedVersion = 1 }),
            ["RestoreProject"] = OrganizationScenario(
                "RestoreProject",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/restore",
                HttpStatusCode.NotFound,
                new { expectedVersion = 1 }),
            ["ListProjectEnvironments"] = OrganizationScenario(
                "ListProjectEnvironments",
                HttpMethod.Get,
                $"/v1/projects/{ShopProjectId}/environments",
                HttpStatusCode.NotFound),
            ["AddProjectEnvironment"] = OrganizationScenario(
                "AddProjectEnvironment",
                HttpMethod.Post,
                $"/v1/projects/{ShopProjectId}/environments",
                HttpStatusCode.NotFound,
                new { name = "Development", code = "dev", position = 1, subscriptionId = Guid.NewGuid(), region = "francecentral", isProtected = false, approvers = Array.Empty<string>(), runners = Array.Empty<string>(), description = "", tags = new Dictionary<string, string>(), expectedVersion = 1 }),
            ["UpdateProjectEnvironment"] = OrganizationScenario(
                "UpdateProjectEnvironment",
                HttpMethod.Put,
                $"/v1/projects/{ShopProjectId}/environments/{Guid.NewGuid():D}",
                HttpStatusCode.NotFound,
                new { name = "Development", code = "dev", subscriptionId = Guid.NewGuid(), region = "francecentral", isProtected = false, approvers = Array.Empty<string>(), runners = Array.Empty<string>(), description = "", tags = new Dictionary<string, string>(), confirmTargetImpact = false, expectedVersion = 1 }),
            ["MoveProjectEnvironment"] = OrganizationScenario(
                "MoveProjectEnvironment",
                HttpMethod.Patch,
                $"/v1/projects/{ShopProjectId}/environments/{Guid.NewGuid():D}/position",
                HttpStatusCode.NotFound,
                new { position = 1, expectedVersion = 1 }),
            ["DeleteProjectEnvironment"] = OrganizationScenario(
                "DeleteProjectEnvironment",
                HttpMethod.Delete,
                $"/v1/projects/{ShopProjectId}/environments/{Guid.NewGuid():D}",
                HttpStatusCode.NotFound,
                new { confirmDeletion = false, expectedVersion = 1 }),
            ["EnqueueDevelopmentPingJob"] = OrganizationScenario(
                "EnqueueDevelopmentPingJob",
                HttpMethod.Post,
                "/v1/dev/ping-job?delayMilliseconds=0",
                HttpStatusCode.NotFound),
            ["GetDevelopmentPingJobStatus"] = OrganizationScenario(
                "GetDevelopmentPingJobStatus",
                HttpMethod.Get,
                $"/v1/dev/ping-job/{TestJobId}",
                HttpStatusCode.NotFound)
        };

    private static IsolationScenario Scenario(
        string name,
        HttpMethod method,
        string path,
        HttpStatusCode expectedStatusCode,
        object? body = null) =>
        new(name, _ => CreateRequest(method, path, null, body), expectedStatusCode);

    private static IsolationScenario AuthenticatedScenario(
        string name,
        HttpMethod method,
        string path,
        HttpStatusCode expectedStatusCode,
        object? body = null) =>
        new(name, _ => CreateRequest(method, path, CreateDavidToken(), body), expectedStatusCode);

    private static IsolationScenario OrganizationScenario(
        string name,
        HttpMethod method,
        string path,
        HttpStatusCode expectedStatusCode,
        object? body = null) =>
        new(name, _ =>
        {
            var request = CreateRequest(method, path, CreateDavidToken(), body);
            request.Headers.Add("X-Ifs-Organization", ContosoOrganizationId);
            return request;
        }, expectedStatusCode);

    private static IsolationScenario OrganizationWriteScenario(
        string name,
        HttpMethod method,
        string path,
        HttpStatusCode expectedStatusCode,
        object? body = null) =>
        new(name, _ =>
        {
            var request = CreateRequest(method, path, CreateDavidToken(), body);
            request.Headers.Add("X-Ifs-Organization", ContosoOrganizationId);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            return request;
        }, expectedStatusCode);

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string? token, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static string CreateDavidToken() => TestTokens.CreateAccessToken(
        tenantId: DavidTenantId,
        objectId: DavidObjectId,
        displayName: "David Moreau",
        email: "david@fabrikam.example");
}
