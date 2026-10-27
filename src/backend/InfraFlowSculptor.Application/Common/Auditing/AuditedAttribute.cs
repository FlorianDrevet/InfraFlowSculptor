using InfraFlowSculptor.Domain.Common;

namespace InfraFlowSculptor.Application.Common.Auditing;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuditedAttribute(string action, ObjectType objectType) : Attribute
{
    public string Action { get; } = action;

    public ObjectType ObjectType { get; } = objectType;
}
