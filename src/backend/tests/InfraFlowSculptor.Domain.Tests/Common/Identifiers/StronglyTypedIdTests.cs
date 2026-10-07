using InfraFlowSculptor.Domain.Common.Identifiers;

namespace InfraFlowSculptor.Domain.Tests.Common.Identifiers;

public sealed class StronglyTypedIdTests
{
    [Fact]
    public void NewUsesTheInjectedClockToCreateAGuidV7()
    {
        var now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);

        var id = OrganizationId.New(clock);

        var expectedTimestamp = Guid.CreateVersion7(now).ToString("N")[..12];
        var value = id.Value.ToString("N");

        Assert.Equal(expectedTimestamp, value[..12]);
        Assert.Equal('7', value[12]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
