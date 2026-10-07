using InfraFlowSculptor.Domain.Common.Identifiers;
using InfraFlowSculptor.Domain.Common.Models;

namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class OrganizationJobBudgetEntity : IOrganizationOwned
{
    public OrganizationId OrganizationId { get; set; }

    public int JobsPerMinute { get; set; }

    public DateTimeOffset WindowStartedAt { get; set; }

    public int JobsProcessed { get; set; }
}
