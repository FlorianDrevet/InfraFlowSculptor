using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.PublicationAggregate;
using InfraFlowSculptor.Domain.RevisionAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Publications;

public sealed class PublicationAggregateConfiguration : IEntityTypeConfiguration<PublicationAggregate>
{
    public void Configure(EntityTypeBuilder<PublicationAggregate> builder)
    {
        builder.ToTable("publications");
        builder.HasKey(publication => publication.Id).HasName("pk_publications");
        builder.Property(publication => publication.Id).ValueGeneratedNever();
        builder.Property(publication => publication.OrganizationId).IsRequired();
        builder.Property(publication => publication.ProjectId).IsRequired();
        builder.Property(publication => publication.RevisionId).IsRequired();
        builder.Property(publication => publication.RevisionNumber).IsRequired();
        builder.Property(publication => publication.AuthorTenantId).IsRequired();
        builder.Property(publication => publication.AuthorObjectId).IsRequired();
        builder.Property(publication => publication.RequestedAt).IsRequired();
        builder.Property(publication => publication.PreparedAt);
        builder.Property(publication => publication.CompletedAt);
        builder.Property(publication => publication.Message).HasMaxLength(500).IsRequired();
        builder.Property(publication => publication.State).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(publication => publication.Version).IsConcurrencyToken();
        builder.HasIndex(publication => new { publication.OrganizationId, publication.ProjectId, publication.RequestedAt })
            .HasDatabaseName("ix_publications_organization_project_requested_at");
        builder.HasIndex(publication => new { publication.ProjectId, publication.State })
            .HasDatabaseName("ix_publications_project_state");
        builder.HasOne<ProjectAggregate>()
            .WithMany()
            .HasForeignKey(publication => publication.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RevisionAggregate>()
            .WithMany()
            .HasForeignKey(publication => publication.RevisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(publication => publication.Targets, targets =>
        {
            targets.ToTable("publication_targets");
            targets.WithOwner().HasForeignKey("PublicationId");
            targets.HasKey("PublicationId", nameof(PublicationTarget.Id)).HasName("pk_publication_targets");
            targets.Property(target => target.Id).ValueGeneratedNever();
            targets.Property(target => target.GitConnectionId).IsRequired();
            targets.Property(target => target.ProviderRepositoryId).HasMaxLength(300).IsRequired();
            targets.Property(target => target.RepositoryName).HasMaxLength(300).IsRequired();
            targets.Property(target => target.DefaultBranch).HasMaxLength(240).IsRequired();
            targets.Property(target => target.BaseCommitSha).HasMaxLength(40).IsRequired();
            targets.Property(target => target.TargetBranch).HasMaxLength(240).IsRequired();
            targets.Property(target => target.SourceBranch).HasMaxLength(240).IsRequired();
            targets.Property(target => target.CommitSha).HasMaxLength(40);
            targets.Property(target => target.PullRequestId).HasMaxLength(200);
            targets.Property(target => target.PullRequestUrl).HasMaxLength(2000);
            targets.Property(target => target.MergeCommitSha).HasMaxLength(40);
            targets.Property(target => target.State).HasConversion<string>().HasMaxLength(32).IsRequired();
            targets.Property(target => target.PullRequestState).HasConversion<string>().HasMaxLength(16).IsRequired();
            targets.Property(target => target.RequiresConfirmation).IsRequired();
            targets.Property(target => target.OwnershipConfirmed).IsRequired();
            targets.Property(target => target.FailureCode).HasMaxLength(64);
            targets.Property(target => target.PreparedAt);
            targets.Property(target => target.CompletedAt);
            targets.Property(target => target.LastTrackedAt);
            targets.HasOne<GitConnectionAggregate>()
                .WithMany()
                .HasForeignKey(target => target.GitConnectionId)
                .OnDelete(DeleteBehavior.Restrict);

            targets.OwnsMany(target => target.Destinations, destinations =>
            {
                destinations.ToTable("publication_target_destinations");
                destinations.WithOwner().HasForeignKey("PublicationId", "TargetId");
                destinations.HasKey("PublicationId", "TargetId", nameof(PublicationTargetDestination.DestinationId))
                    .HasName("pk_publication_target_destinations");
                destinations.Property(destination => destination.DestinationId).ValueGeneratedNever();
                destinations.Property(destination => destination.BasePath).HasMaxLength(200).IsRequired();
            });
            targets.Navigation(target => target.Destinations).UsePropertyAccessMode(PropertyAccessMode.Field);

            targets.OwnsMany(target => target.Files, files =>
            {
                files.ToTable("publication_target_files");
                files.WithOwner().HasForeignKey("PublicationId", "TargetId");
                files.HasKey("PublicationId", "TargetId", nameof(PublicationTargetFile.Id))
                    .HasName("pk_publication_target_files");
                files.Property(file => file.Id).ValueGeneratedNever();
                files.Property(file => file.DestinationId).IsRequired();
                files.Property(file => file.RepositoryPath).HasMaxLength(1024).IsRequired();
                files.Property(file => file.Sha256).HasMaxLength(64);
                files.Property(file => file.Action).HasConversion<string>().HasMaxLength(16).IsRequired();
                files.Property(file => file.ExistingSha256).HasMaxLength(64);
            });
            targets.Navigation(target => target.Files).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(publication => publication.Targets).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
