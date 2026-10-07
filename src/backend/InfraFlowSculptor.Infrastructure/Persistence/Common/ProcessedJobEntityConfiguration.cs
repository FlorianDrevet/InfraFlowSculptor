using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class ProcessedJobEntityConfiguration : IEntityTypeConfiguration<ProcessedJobEntity>
{
    public void Configure(EntityTypeBuilder<ProcessedJobEntity> builder)
    {
        builder.ToTable("processed_jobs");
        builder.HasKey(job => new { job.JobId, job.HandlerType });
        builder.Property(job => job.HandlerType).HasMaxLength(500).IsRequired();
        builder.Property(job => job.ProcessedAt).IsRequired();
    }
}
