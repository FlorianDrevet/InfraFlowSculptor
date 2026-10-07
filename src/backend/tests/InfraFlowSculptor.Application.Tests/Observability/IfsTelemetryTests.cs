using System.Diagnostics.Metrics;
using InfraFlowSculptor.Application.Common.Observability;
using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Application.Tests.Observability;

public sealed class IfsTelemetryTests
{
    [Fact]
    public void LoggingScopeUsesOpaqueIdentifiersAndTraceId()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        const string projectId = "project-prod-42";
        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";

        var scope = IfsTelemetry.CreateLogScope(organizationId, projectId, traceId);

        Assert.Equal(IfsTelemetry.Pseudonymize(organizationId.Value), scope["OrganizationId"]);
        Assert.NotEqual(projectId, scope["ProjectId"]);
        Assert.Equal(IfsTelemetry.Pseudonymize(projectId), scope["ProjectId"]);
        Assert.Equal(traceId, scope["TraceId"]);
        Assert.Equal(3, scope.Count);
    }

    [Fact]
    public void JobCounterRecordsOnlyOpaqueOrganizationIdentifier()
    {
        var organizationId = new OrganizationId(Guid.NewGuid());
        var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == IfsTelemetry.MeterName && instrument.Name == "ifs.jobs")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "ifs.jobs")
            {
                measurements.Add((value, tags.ToArray()));
            }
        });
        listener.Start();

        IfsTelemetry.RecordJob(organizationId);

        var measurement = Assert.Single(measurements);
        Assert.Equal(1, measurement.Value);
        var tag = Assert.Single(measurement.Tags);
        Assert.Equal("organization_id", tag.Key);
        Assert.Equal(IfsTelemetry.Pseudonymize(organizationId.Value), tag.Value);
        Assert.NotEqual(organizationId.Value.ToString("D"), tag.Value);
    }
}
