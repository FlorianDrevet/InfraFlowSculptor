using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using InfraFlowSculptor.Domain.Common.Identifiers;
using Microsoft.Extensions.Logging;

namespace InfraFlowSculptor.Application.Common.Observability;

public static class IfsTelemetry
{
    public const string ActivitySourceName = "InfraFlowSculptor";
    public const string MeterName = "InfraFlowSculptor";

    private const string OrganizationTagName = "organization_id";

    private static readonly ActivitySource Source = new(ActivitySourceName);
    private static readonly Meter MetricMeter = new(MeterName);
    private static readonly Counter<long> Generations = MetricMeter.CreateCounter<long>(
        "ifs.generations",
        "1",
        "Generation operations started.");
    private static readonly Counter<long> Publications = MetricMeter.CreateCounter<long>(
        "ifs.publications",
        "1",
        "Publication operations started.");
    private static readonly Counter<long> OutputCheckFailures = MetricMeter.CreateCounter<long>(
        "ifs.output_check_failures",
        "1",
        "Output validation failures.");
    private static readonly Counter<long> Jobs = MetricMeter.CreateCounter<long>(
        "ifs.jobs",
        "1",
        "Successfully processed background jobs.");

    public static ActivitySource ActivitySource => Source;

    public static void RecordGeneration(OrganizationId organizationId) => Record(Generations, organizationId);

    public static void RecordPublication(OrganizationId organizationId) => Record(Publications, organizationId);

    public static void RecordOutputCheckFailure(OrganizationId organizationId) => Record(OutputCheckFailures, organizationId);

    public static void RecordJob(OrganizationId organizationId) => Record(Jobs, organizationId);

    public static string Pseudonymize(Guid identifier)
    {
        Span<byte> identifierBytes = stackalloc byte[16];
        identifier.TryWriteBytes(identifierBytes);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(identifierBytes, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Pseudonymize(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        if (Guid.TryParse(identifier, out var guid))
        {
            return Pseudonymize(guid);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identifier))).ToLowerInvariant();
    }

    public static IReadOnlyDictionary<string, object?> CreateLogScope(
        OrganizationId? organizationId,
        string? projectId = null,
        string? traceId = null)
    {
        return new Dictionary<string, object?>(3)
        {
            ["OrganizationId"] = organizationId is { } id ? Pseudonymize(id.Value) : null,
            ["ProjectId"] = string.IsNullOrWhiteSpace(projectId) ? null : Pseudonymize(projectId),
            ["TraceId"] = traceId ?? Activity.Current?.TraceId.ToString()
        };
    }

    public static IDisposable? BeginLogScope(
        ILogger logger,
        OrganizationId? organizationId,
        string? projectId = null,
        string? traceId = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return logger.BeginScope(CreateLogScope(organizationId, projectId, traceId));
    }

    public static void SetCurrentActivityTags(OrganizationId? organizationId, string? projectId = null)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        if (organizationId is { } id)
        {
            activity.SetTag(OrganizationTagName, Pseudonymize(id.Value));
        }

        if (!string.IsNullOrWhiteSpace(projectId))
        {
            activity.SetTag("project_id", Pseudonymize(projectId));
        }
    }

    private static void Record(Counter<long> counter, OrganizationId organizationId)
    {
        var tags = new TagList();
        tags.Add(OrganizationTagName, Pseudonymize(organizationId.Value));
        counter.Add(1, tags);
    }
}
