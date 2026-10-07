using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Domain.Tests.Common;

public sealed class ValueObjectTests
{
    [Fact]
    public void EqualValueObjectsHaveEqualValuesAndHashCodes()
    {
        var first = new SampleValueObject("project", 7);
        var second = new SampleValueObject("project", 7);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void DifferentValueObjectsAreNotEqual()
    {
        var first = new SampleValueObject("project", 7);
        var second = new SampleValueObject("project", 8);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EqualEnumValueObjectsHaveEqualValuesAndHashCodes()
    {
        var first = new SampleEnumValueObject(SampleStatus.Active);
        var second = new SampleEnumValueObject(SampleStatus.Active);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, new SampleEnumValueObject(SampleStatus.Archived));
    }

    private sealed class SampleValueObject(string name, int rank) : ValueObject
    {
        public override IEnumerable<object> GetEqualityComponents()
        {
            yield return name;
            yield return rank;
        }
    }

    private sealed class SampleEnumValueObject(SampleStatus value) : EnumValueObject<SampleStatus>(value)
    {
    }

    private enum SampleStatus
    {
        Active,
        Archived
    }
}
