using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class UserIdempotencyKeyEntityConfiguration : IEntityTypeConfiguration<UserIdempotencyKeyEntity>
{
    public void Configure(EntityTypeBuilder<UserIdempotencyKeyEntity> builder)
    {
        builder.ToTable("user_idempotency_keys");
        builder.HasKey(key => new { key.TenantId, key.UserObjectId, key.Key });
        builder.Property(key => key.TenantId).IsRequired();
        builder.Property(key => key.UserObjectId).IsRequired();
        builder.Property(key => key.Key).ValueGeneratedNever();
        builder.Property(key => key.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(key => key.StatusCode);
        builder.Property(key => key.Response).HasColumnType("text");
        builder.Property(key => key.CreatedAt).IsRequired();
        builder.HasIndex(key => key.CreatedAt);
    }
}
