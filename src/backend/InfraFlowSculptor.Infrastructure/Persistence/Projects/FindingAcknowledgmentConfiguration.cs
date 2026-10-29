using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Projects;

public sealed class FindingAcknowledgmentConfiguration : IEntityTypeConfiguration<FindingAcknowledgmentAggregate>
{
    public void Configure(EntityTypeBuilder<FindingAcknowledgmentAggregate> builder)
    {
        builder.ToTable("finding_acknowledgments");
        builder.HasKey(acknowledgment => acknowledgment.Id).HasName("pk_finding_acknowledgments");
        builder.Property(acknowledgment => acknowledgment.Id).ValueGeneratedNever();
        builder.Property(acknowledgment => acknowledgment.ProjectId).IsRequired();
        builder.Property(acknowledgment => acknowledgment.OrganizationId).IsRequired();
        builder.Property(acknowledgment => acknowledgment.FindingCode).HasMaxLength(64).IsRequired();
        builder.Property(acknowledgment => acknowledgment.DetailKey).HasMaxLength(128);
        builder.Property(acknowledgment => acknowledgment.SubjectType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(acknowledgment => acknowledgment.SubjectId).IsRequired();
        builder.Property(acknowledgment => acknowledgment.EnvironmentId);
        builder.Property(acknowledgment => acknowledgment.ObjectVersion).IsRequired();
        builder.Property(acknowledgment => acknowledgment.Comment).HasMaxLength(2000).IsRequired();
        builder.Property(acknowledgment => acknowledgment.AcknowledgedBy).IsRequired();
        builder.Property(acknowledgment => acknowledgment.CreatedAt).IsRequired();
        builder.HasIndex(acknowledgment => new
            {
                acknowledgment.ProjectId,
                acknowledgment.FindingCode,
                acknowledgment.SubjectType,
                acknowledgment.SubjectId,
                acknowledgment.EnvironmentId,
                acknowledgment.ObjectVersion,
                acknowledgment.DetailKey
            })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_finding_acknowledgments_identity");
        builder.HasIndex(acknowledgment => new
            {
                acknowledgment.ProjectId,
                acknowledgment.FindingCode,
                acknowledgment.SubjectType,
                acknowledgment.SubjectId,
                acknowledgment.EnvironmentId,
                acknowledgment.ObjectVersion,
                acknowledgment.CreatedAt
            })
            .HasDatabaseName("ix_finding_acknowledgments_active_lookup");
        builder.HasOne<ProjectAggregate>()
            .WithMany()
            .HasForeignKey(acknowledgment => acknowledgment.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<OrganizationAggregate>()
            .WithMany()
            .HasForeignKey(acknowledgment => acknowledgment.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(acknowledgment => acknowledgment.AcknowledgedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
