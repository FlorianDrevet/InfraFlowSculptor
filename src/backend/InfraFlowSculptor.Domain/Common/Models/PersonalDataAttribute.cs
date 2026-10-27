namespace InfraFlowSculptor.Domain.Common.Models;

[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class PersonalDataAttribute(string field) : Attribute
{
    public string Field { get; } = field;
}
