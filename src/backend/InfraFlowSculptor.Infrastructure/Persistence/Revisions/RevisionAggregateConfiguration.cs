using InfraFlowSculptor.Domain.RevisionAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Revisions;

public sealed class RevisionAggregateConfiguration : IEntityTypeConfiguration<RevisionAggregate>
{
    public void Configure(EntityTypeBuilder<RevisionAggregate> builder)
    {
        builder.ToTable("revisions");
        builder.HasKey(revision => revision.Id).HasName("pk_revisions");
        builder.Property(revision => revision.Id).ValueGeneratedNever();
        builder.Property(revision => revision.OrganizationId).IsRequired();
        builder.Property(revision => revision.ProjectId).IsRequired();
        builder.Property(revision => revision.Number).IsRequired();
        builder.Property(revision => revision.AuthorTenantId).IsRequired();
        builder.Property(revision => revision.AuthorObjectId).IsRequired();
        builder.Property(revision => revision.CreatedAt).IsRequired();
        builder.Property(revision => revision.ModelVersion).IsRequired();
        builder.Property(revision => revision.CatalogVersion).HasMaxLength(64).IsRequired();
        builder.Property(revision => revision.InfrastructureLanguage).HasMaxLength(32).IsRequired();
        builder.Property(revision => revision.CiPlatform).HasMaxLength(32).IsRequired();
        builder.Property(revision => revision.InputSha256).HasMaxLength(64).IsRequired();
        builder.Property(revision => revision.State).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(revision => revision.FailureCode).HasMaxLength(64);
        builder.Property(revision => revision.DeploymentPlanJson).HasColumnType("text").IsRequired();
        builder.HasIndex(revision => new { revision.ProjectId, revision.Number })
            .IsUnique()
            .HasDatabaseName("ux_revisions_project_number");
        builder.HasIndex(revision => new { revision.OrganizationId, revision.State, revision.CreatedAt })
            .HasDatabaseName("ix_revisions_organization_state_created_at");
        builder.HasIndex(revision => new { revision.ProjectId, revision.InputSha256, revision.Number })
            .HasDatabaseName("ix_revisions_project_input_number");
        builder.HasOne<InfraFlowSculptor.Domain.ProjectAggregate.ProjectAggregate>()
            .WithMany()
            .HasForeignKey(revision => revision.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(revision => revision.Files, files =>
        {
            files.ToTable("revision_files");
            files.WithOwner().HasForeignKey("revision_id");
            files.Property(file => file.RelativePath).HasMaxLength(1024).IsRequired();
            files.Property(file => file.Sha256).HasMaxLength(64).IsRequired();
            files.Property(file => file.Size).IsRequired();
            files.HasKey("revision_id", nameof(RevisionFile.RelativePath)).HasName("pk_revision_files");
            files.HasIndex(nameof(RevisionFile.Sha256)).HasDatabaseName("ix_revision_files_sha256");
        });

        builder.OwnsMany(revision => revision.Findings, findings =>
        {
            findings.ToTable("revision_findings");
            findings.WithOwner().HasForeignKey("revision_id");
            findings.Property<Guid>("id");
            findings.Property(finding => finding.Code).HasMaxLength(64).IsRequired();
            findings.Property(finding => finding.Severity).HasConversion<string>().HasMaxLength(16).IsRequired();
            findings.Property(finding => finding.MessageFr).HasMaxLength(2000).IsRequired();
            findings.Property(finding => finding.MessageEn).HasMaxLength(2000).IsRequired();
            findings.Property(finding => finding.SubjectType).HasMaxLength(32).IsRequired();
            findings.Property(finding => finding.SubjectId).IsRequired();
            findings.Property(finding => finding.EnvironmentId);
            findings.HasKey("revision_id", "id").HasName("pk_revision_findings");
        });

        builder.OwnsMany(revision => revision.Changes, changes =>
        {
            changes.ToTable("revision_changes");
            changes.WithOwner().HasForeignKey("revision_id");
            changes.Property<int>("position");
            changes.Property(change => change.Category).HasMaxLength(32).IsRequired();
            changes.Property(change => change.Subject).HasMaxLength(512).IsRequired();
            changes.Property(change => change.DescriptionFr).HasMaxLength(2000).IsRequired();
            changes.Property(change => change.DescriptionEn).HasMaxLength(2000).IsRequired();
            changes.HasKey("revision_id", "position").HasName("pk_revision_changes");
        });
    }
}
