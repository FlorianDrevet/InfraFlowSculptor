namespace InfraFlowSculptor.Domain.Common.Models;

public abstract class EnumValueObject<TEnum> : ValueObject where TEnum : struct, Enum
{
    public TEnum Value { get; protected set; }

    protected EnumValueObject()
    {
    }

    protected EnumValueObject(TEnum value)
    {
        Value = value;
    }

#pragma warning disable CA1000 // These helpers are intentionally scoped to the enum value type.
    public static TEnum ParseOrDefault(string? value, TEnum defaultValue)
    {
        return Enum.TryParse(value, ignoreCase: true, out TEnum result) ? result : defaultValue;
    }

    public static TEnum GetDefaultValue() => default;
#pragma warning restore CA1000

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
