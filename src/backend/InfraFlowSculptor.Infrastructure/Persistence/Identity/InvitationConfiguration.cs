using InfraFlowSculptor.Domain.Invitations;
using InfraFlowSculptor.Domain.OrganizationAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Identity;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<InvitationAggregate>
{
    public void Configure(EntityTypeBuilder<InvitationAggregate> builder)
    {
        builder.ToTable("organization_invitations");
        builder.HasKey(invitation => invitation.Id).HasName("pk_organization_invitations");
        builder.Property(invitation => invitation.Id).ValueGeneratedNever();
        builder.Property(invitation => invitation.OrganizationId).IsRequired();
        builder.Property(invitation => invitation.Email).HasMaxLength(320).IsRequired();
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_organization_invitations_token_hash");
        builder.Property(invitation => invitation.RoleValues)
            .HasColumnName("roles")
            .HasColumnType("integer[]")
            .IsRequired();
        builder.Property(invitation => invitation.CreatedAt).IsRequired();
        builder.Property(invitation => invitation.ExpiresAt).IsRequired();
        builder.Property(invitation => invitation.Status).IsRequired();
        builder.Property(invitation => invitation.Language).HasMaxLength(2).IsRequired();
        builder.Property(invitation => invitation.Version).IsConcurrencyToken();
        builder.HasOne<OrganizationAggregate>()
            .WithMany()
            .HasForeignKey(invitation => invitation.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_organization_invitations_organizations_organization_id");
    }
}
