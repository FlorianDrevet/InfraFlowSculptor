using InfraFlowSculptor.Domain.OrganizationAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Auditing;

public sealed class AuditEventEntityConfiguration : IEntityTypeConfiguration<AuditEventEntity>
{
    public void Configure(EntityTypeBuilder<AuditEventEntity> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(auditEvent => auditEvent.Id).HasName("pk_audit_events");
        builder.Property(auditEvent => auditEvent.Id).ValueGeneratedNever();
        builder.Property(auditEvent => auditEvent.OrganizationId).IsRequired();
        builder.Property(auditEvent => auditEvent.ProjectId);
        builder.Property(auditEvent => auditEvent.OccurredAt).IsRequired();
        builder.Property(auditEvent => auditEvent.ActorKind).HasMaxLength(32).IsRequired();
        builder.Property(auditEvent => auditEvent.ActorRef).HasMaxLength(128).IsRequired();
        builder.Property(auditEvent => auditEvent.Via).HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(auditEvent => auditEvent.Action).HasMaxLength(128).IsRequired();
        builder.Property(auditEvent => auditEvent.ObjectType).HasMaxLength(32).HasConversion<string>().IsRequired();
        builder.Property(auditEvent => auditEvent.ObjectId).IsRequired();
        builder.Property(auditEvent => auditEvent.Diff).HasColumnType("text").IsRequired();
        builder.HasIndex(auditEvent => new { auditEvent.OrganizationId, auditEvent.OccurredAt })
            .HasDatabaseName("ix_audit_events_organization_id_occurred_at");
        builder.HasOne<OrganizationAggregate>()
            .WithMany()
            .HasForeignKey(auditEvent => auditEvent.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_events_organizations_organization_id");
        builder.HasMany<AuditPersonalDataEntity>()
            .WithOne(personalData => personalData.AuditEvent)
            .HasForeignKey(personalData => personalData.EventId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_audit_personal_data_audit_events_event_id");
    }
}
