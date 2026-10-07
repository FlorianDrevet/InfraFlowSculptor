using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class IdempotencyKeyEntityConfiguration : IEntityTypeConfiguration<IdempotencyKeyEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyEntity> builder)
    {
        builder.ToTable("idempotency_keys");
        builder.HasKey(key => new { key.OrganizationId, key.Key });
        builder.Property(key => key.OrganizationId).IsRequired();
        builder.Property(key => key.Key).ValueGeneratedNever();
        builder.Property(key => key.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(key => key.StatusCode);
        builder.Property(key => key.Response).HasColumnType("jsonb");
        builder.Property(key => key.CreatedAt).IsRequired();
        builder.HasIndex(key => key.CreatedAt);
    }
}
