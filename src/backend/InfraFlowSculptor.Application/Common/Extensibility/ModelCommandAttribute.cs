namespace InfraFlowSculptor.Application.Common.Extensibility;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ModelCommandAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
