using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class OrganizationJobBudgetEntityConfiguration : IEntityTypeConfiguration<OrganizationJobBudgetEntity>
{
    public void Configure(EntityTypeBuilder<OrganizationJobBudgetEntity> builder)
    {
        builder.ToTable("organization_budgets");
        builder.HasKey(budget => budget.OrganizationId);
        builder.Property(budget => budget.OrganizationId).IsRequired();
        builder.Property(budget => budget.JobsPerMinute).IsRequired();
        builder.Property(budget => budget.WindowStartedAt).IsRequired();
        builder.Property(budget => budget.JobsProcessed).IsRequired();
    }
}
