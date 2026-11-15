using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Projects;

public sealed class InstallationChecklistProgressConfiguration : IEntityTypeConfiguration<InstallationChecklistProgressAggregate>
{
    public void Configure(EntityTypeBuilder<InstallationChecklistProgressAggregate> builder)
    {
        builder.ToTable("installation_checklist_progress");
        builder.HasKey(progress => progress.Id).HasName("pk_installation_checklist_progress");
        builder.Property(progress => progress.Id).ValueGeneratedNever();
        builder.Property(progress => progress.ProjectId).IsRequired();
        builder.Property(progress => progress.OrganizationId).IsRequired();
        builder.Property(progress => progress.ItemKey).HasMaxLength(160).IsRequired();
        builder.Property(progress => progress.CompletedBy);
        builder.Property(progress => progress.CompletedAt);
        builder.HasIndex(progress => new { progress.ProjectId, progress.ItemKey })
            .IsUnique()
            .HasDatabaseName("ux_installation_checklist_progress_project_item");
        builder.HasOne<ProjectAggregate>()
            .WithMany()
            .HasForeignKey(progress => progress.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<OrganizationAggregate>()
            .WithMany()
            .HasForeignKey(progress => progress.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(progress => progress.CompletedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
