using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
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
            ["GetFeatures"] = AuthenticatedScenario("GetFeatures", HttpMethod.Get, "/v1/features", HttpStatusCode.OK),
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
