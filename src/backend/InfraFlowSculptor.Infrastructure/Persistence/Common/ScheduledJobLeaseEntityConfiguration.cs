using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class ScheduledJobLeaseEntityConfiguration : IEntityTypeConfiguration<ScheduledJobLeaseEntity>
{
    public void Configure(EntityTypeBuilder<ScheduledJobLeaseEntity> builder)
    {
        builder.ToTable("scheduled_job_leases");
        builder.HasKey(lease => lease.JobName);
        builder.Property(lease => lease.JobName).HasMaxLength(200).IsRequired();
        builder.Property(lease => lease.Holder).HasMaxLength(200).IsRequired();
        builder.Property(lease => lease.ExpiresAt).IsRequired();
    }
}
