using InfraFlowSculptor.Domain.ProjectAggregate;
using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Identity;

public sealed class UserProjectFavoriteConfiguration : IEntityTypeConfiguration<UserProjectFavorite>
{
    public void Configure(EntityTypeBuilder<UserProjectFavorite> builder)
    {
        builder.ToTable("user_project_favorites");
        builder.HasKey(favorite => new { favorite.UserProfileId, favorite.ProjectId })
            .HasName("pk_user_project_favorites");
        builder.Property(favorite => favorite.UserProfileId).IsRequired();
        builder.Property(favorite => favorite.ProjectId).IsRequired();
        builder.HasOne<ProjectAggregate>()
            .WithMany()
            .HasForeignKey(favorite => favorite.ProjectId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_project_favorites_projects_project_id");
    }
}
