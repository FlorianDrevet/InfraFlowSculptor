using InfraFlowSculptor.Domain.OrganizationAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Identity;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<OrganizationAggregate>
{
    public void Configure(EntityTypeBuilder<OrganizationAggregate> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(organization => organization.Id).HasName("pk_organizations");
        builder.Property(organization => organization.Name).HasMaxLength(80).IsRequired();
        builder.Property(organization => organization.Version).IsConcurrencyToken();
        builder.Property<string[]>("allowedEmailDomains")
            .HasField("allowedEmailDomains")
            .HasColumnName("allowed_email_domains")
            .HasColumnType("text[]")
            .IsRequired();
        builder.Property<Guid[]>("allowedTenantIds")
            .HasField("allowedTenantIds")
            .HasColumnName("allowed_tenant_ids")
            .HasColumnType("uuid[]")
            .IsRequired();

        builder.OwnsMany(organization => organization.Members, members =>
        {
            members.ToTable("organization_members");
            members.WithOwner().HasForeignKey("OrganizationId");
            members.HasKey("OrganizationId", nameof(OrganizationMember.UserProfileId))
                .HasName("pk_organization_members");
            members.Property(member => member.UserProfileId).IsRequired();
            members.Property(member => member.RoleValues)
                .HasColumnName("roles")
                .HasColumnType("integer[]")
                .IsRequired();
        });

        builder.Navigation(organization => organization.Members)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
