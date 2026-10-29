using InfraFlowSculptor.Application.Naming.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Naming;

public sealed class NameAvailabilityEntityConfiguration : IEntityTypeConfiguration<NameAvailabilityEntity>
{
    public void Configure(EntityTypeBuilder<NameAvailabilityEntity> builder)
    {
        builder.ToTable("name_availability", table =>
        {
            table.HasCheckConstraint(
                "ck_name_availability_status",
                "status IN ('Pending', 'Resolved', 'NotResolved', 'Unknown')");
            table.HasCheckConstraint(
                "ck_name_availability_attempts",
                "attempts >= 0 AND attempts <= 3");
        });
        builder.HasKey(entry => new { entry.AvailabilityDomain, entry.Name })
            .HasName("pk_name_availability");
        builder.Property(entry => entry.AvailabilityDomain)
            .HasColumnName("availability_domain")
            .HasMaxLength(253)
            .IsRequired();
        builder.Property(entry => entry.Name)
            .HasMaxLength(253)
            .IsRequired();
        builder.Property(entry => entry.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(entry => entry.RequestedAt).IsRequired();
        builder.Property(entry => entry.NextAttemptAt).IsRequired();
        builder.Property(entry => entry.CheckedAt);
        builder.Property(entry => entry.ExpiresAt);
        builder.Property(entry => entry.Attempts).HasDefaultValue(0).IsRequired();
        builder.Property(entry => entry.Failure)
            .HasColumnName("failure_code")
            .HasConversion<string?>()
            .HasMaxLength(32);
        builder.HasIndex(entry => new { entry.Status, entry.NextAttemptAt })
            .HasDatabaseName("ix_name_availability_status_next_attempt_at");
        builder.HasIndex(entry => entry.ExpiresAt)
            .HasDatabaseName("ix_name_availability_expires_at");
    }
}
