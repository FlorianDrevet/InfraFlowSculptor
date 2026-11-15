using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.GitConnections;

public sealed class GitConnectionPublicationUseConfiguration : IEntityTypeConfiguration<GitConnectionPublicationUse>
{
    public void Configure(EntityTypeBuilder<GitConnectionPublicationUse> builder)
    {
        builder.ToTable("git_connection_publication_uses");
        builder.HasKey(use => new { use.ConnectionId, use.ProjectId })
            .HasName("pk_git_connection_publication_uses");
        builder.Property(use => use.OrganizationId).IsRequired();
        builder.HasOne<GitConnectionAggregate>()
            .WithMany()
            .HasForeignKey(use => use.ConnectionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectAggregate>()
            .WithMany()
            .HasForeignKey(use => use.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
