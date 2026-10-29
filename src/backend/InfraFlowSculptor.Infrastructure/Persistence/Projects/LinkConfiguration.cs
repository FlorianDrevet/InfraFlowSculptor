using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Projects;

public sealed class LinkConfiguration : IEntityTypeConfiguration<LinkAggregate>
{
    public void Configure(EntityTypeBuilder<LinkAggregate> builder)
    {
        builder.ToTable("project_links");
        builder.HasKey(link => link.Id).HasName("pk_project_links");
        builder.Property(link => link.Id).ValueGeneratedNever();
        builder.Property(link => link.ProjectId).IsRequired();
        builder.Property(link => link.OrganizationId).IsRequired();
        builder.Property(link => link.SourceComponentId).IsRequired();
        builder.Property(link => link.SourceResourceId).IsRequired();
        builder.Property(link => link.TargetComponentId).IsRequired();
        builder.Property(link => link.TargetResourceId).IsRequired();
        builder.Property(link => link.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(link => link.Origin).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(link => link.IdentityKind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(link => link.IdentityResourceId);
        builder.Property(link => link.AccessScope).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(link => link.DataAccessLevel).HasConversion<string>().HasMaxLength(32);
        builder.Property(link => link.Version).IsConcurrencyToken();
        builder.HasIndex(link => new { link.ProjectId, link.SourceResourceId })
            .HasDatabaseName("ix_project_links_source");
        builder.HasIndex(link => new { link.ProjectId, link.TargetResourceId })
            .HasDatabaseName("ix_project_links_target");
        builder.HasOne<ProjectAggregate>()
            .WithMany()
            .HasForeignKey(link => link.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(link => link.Roles, roles =>
        {
            roles.ToTable("project_link_roles");
            roles.WithOwner().HasForeignKey("LinkId");
            roles.HasKey("LinkId", nameof(LinkRole.Name)).HasName("pk_project_link_roles");
            roles.Property(role => role.Name).HasMaxLength(128).IsRequired();
        });
        builder.Navigation(link => link.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
