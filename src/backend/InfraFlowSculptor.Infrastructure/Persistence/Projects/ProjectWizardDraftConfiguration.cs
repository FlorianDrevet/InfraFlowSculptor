using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Projects;

public sealed class ProjectWizardDraftConfiguration : IEntityTypeConfiguration<ProjectWizardDraft>
{
    public void Configure(EntityTypeBuilder<ProjectWizardDraft> builder)
    {
        builder.ToTable("project_wizard_drafts");
        builder.HasKey(draft => new { draft.OrganizationId, draft.UserProfileId })
            .HasName("pk_project_wizard_drafts");
        builder.Property(draft => draft.ContentJson).HasColumnType("jsonb").IsRequired();
        builder.Property(draft => draft.ExpiresAt).IsRequired();
        builder.HasIndex(draft => draft.ExpiresAt).HasDatabaseName("ix_project_wizard_drafts_expires_at");
        builder.HasOne<OrganizationAggregate>()
            .WithMany()
            .HasForeignKey(draft => draft.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(draft => draft.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
