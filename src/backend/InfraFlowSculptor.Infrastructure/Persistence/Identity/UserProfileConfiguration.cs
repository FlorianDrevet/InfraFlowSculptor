using InfraFlowSculptor.Domain.UserProfileAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Identity;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");
        builder.HasKey(profile => profile.Id).HasName("pk_user_profiles");
        builder.HasIndex(profile => new { profile.TenantId, profile.ObjectId })
            .IsUnique()
            .HasDatabaseName("ux_user_profiles_tenant_id_object_id");
        builder.Property(profile => profile.TenantId).HasColumnName("tenant_id");
        builder.Property(profile => profile.ObjectId).HasColumnName("object_id");
        builder.Ignore(profile => profile.UserKey);
        builder.Property(profile => profile.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(profile => profile.Email).HasMaxLength(320);
        builder.Property(profile => profile.PreferredLanguage).HasMaxLength(2).IsRequired();
        builder.Property(profile => profile.PreferredTheme).HasMaxLength(16).IsRequired();
        builder.Property(profile => profile.CreatedAt).IsRequired();
        builder.Property(profile => profile.LastSeenAt).IsRequired();
        builder.HasMany(profile => profile.FavoriteProjects)
            .WithOne()
            .HasForeignKey(favorite => favorite.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_project_favorites_user_profiles_user_profile_id");
        builder.Navigation(profile => profile.FavoriteProjects)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
