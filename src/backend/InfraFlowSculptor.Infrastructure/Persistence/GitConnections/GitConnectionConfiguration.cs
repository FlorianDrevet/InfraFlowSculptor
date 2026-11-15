using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.GitConnections;

public sealed class GitConnectionConfiguration : IEntityTypeConfiguration<GitConnectionAggregate>
{
    public void Configure(EntityTypeBuilder<GitConnectionAggregate> builder)
    {
        builder.ToTable("git_connections");
        builder.HasKey(connection => connection.Id).HasName("pk_git_connections");
        builder.Property(connection => connection.ProviderKind).IsRequired();
        builder.Property(connection => connection.Name).HasMaxLength(80).IsRequired();
        builder.Property(connection => connection.Organization).HasMaxLength(300).IsRequired();
        builder.Property(connection => connection.TenantId);
        builder.Property(connection => connection.AuthenticationMode).IsRequired();
        builder.Property(connection => connection.PersonalAccessTokenExpiresAt);
        builder.Property(connection => connection.SecretName).HasMaxLength(180);
        builder.Property(connection => connection.Version).IsConcurrencyToken();
        builder.Property(connection => connection.Expiry15DayAlertSent)
            .HasColumnName("expiry_15_day_alert_sent")
            .IsRequired();
        builder.Property(connection => connection.Expiry3DayAlertSent)
            .HasColumnName("expiry_3_day_alert_sent")
            .IsRequired();
        builder.HasOne<OrganizationAggregate>()
            .WithMany()
            .HasForeignKey(connection => connection.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(connection => connection.Repositories, repositories =>
        {
            repositories.ToTable("git_connection_repositories");
            repositories.WithOwner().HasForeignKey("GitConnectionId");
            repositories.HasKey("GitConnectionId", nameof(GitConnectionRepository.ProviderRepositoryId))
                .HasName("pk_git_connection_repositories");
            repositories.Property(repository => repository.ProviderRepositoryId).HasMaxLength(255).IsRequired();
            repositories.Property(repository => repository.Name).HasMaxLength(255).IsRequired();
            repositories.Property(repository => repository.DefaultBranch).HasMaxLength(255).IsRequired();
            repositories.Property(repository => repository.OpenToAllProjects).IsRequired();

            repositories.OwnsMany(repository => repository.AllowedProjects, projects =>
            {
                projects.ToTable("git_connection_repository_projects");
                projects.WithOwner().HasForeignKey("GitConnectionId", "ProviderRepositoryId");
                projects.HasKey(
                        "GitConnectionId",
                        "ProviderRepositoryId",
                        nameof(GitConnectionProjectAccess.ProjectId))
                    .HasName("pk_git_connection_repository_projects");
                projects.Property(project => project.ProjectId).IsRequired();
            });

            repositories.Navigation(repository => repository.AllowedProjects)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Navigation(connection => connection.Repositories)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
