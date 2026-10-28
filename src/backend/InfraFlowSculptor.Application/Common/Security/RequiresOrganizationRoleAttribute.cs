using InfraFlowSculptor.Domain.OrganizationAggregate;

namespace InfraFlowSculptor.Application.Common.Security;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresOrganizationRoleAttribute(params OrganizationRole[] roles) : Attribute
{
    public IReadOnlyCollection<OrganizationRole> Roles { get; } = roles.Length == 0
        ? throw new ArgumentException("At least one organization role is required.", nameof(roles))
        : roles.Distinct().ToArray();

    public string? Permission { get; set; }
}
