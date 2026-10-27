using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Auditing;

public sealed class AuditPersonalDataEntityConfiguration : IEntityTypeConfiguration<AuditPersonalDataEntity>
{
    public void Configure(EntityTypeBuilder<AuditPersonalDataEntity> builder)
    {
        builder.ToTable("audit_personal_data");
        builder.HasKey(personalData => new { personalData.EventId, personalData.Field })
            .HasName("pk_audit_personal_data");
        builder.Property(personalData => personalData.EventId).IsRequired();
        builder.Property(personalData => personalData.SubjectUserId);
        builder.Property(personalData => personalData.Field).HasMaxLength(128).IsRequired();
        builder.Property(personalData => personalData.Value).IsRequired();
        builder.HasIndex(personalData => personalData.SubjectUserId)
            .HasDatabaseName("ix_audit_personal_data_subject_user_id");
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(personalData => personalData.SubjectUserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_audit_personal_data_user_profiles_subject_user_id");
    }
}
