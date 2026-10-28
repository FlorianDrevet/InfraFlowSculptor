using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Identity;

public sealed class OrganizationFeatureAccessConfiguration : IEntityTypeConfiguration<OrganizationFeatureAccessEntity>
{
    public void Configure(EntityTypeBuilder<OrganizationFeatureAccessEntity> builder)
    {
        builder.ToTable("organization_feature_access");
        builder.HasKey(access => new { access.OrganizationId, access.FeatureKey })
            .HasName("pk_organization_feature_access");
        builder.Property(access => access.FeatureKey).HasMaxLength(100).IsRequired();
    }
}
