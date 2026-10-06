using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Common;

public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.OrganizationId).IsRequired();
        builder.Property(message => message.Queue).HasMaxLength(100).IsRequired();
        builder.Property(message => message.SessionId).HasMaxLength(200).IsRequired();
        builder.Property(message => message.Type).HasMaxLength(500).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.CreatedAt).IsRequired();
        builder.Property(message => message.FailedAt);
        builder.Property(message => message.Attempts).HasDefaultValue(0).IsRequired();
        builder.Property(message => message.NextAttemptAt);
        builder.HasIndex(message => new { message.OrganizationId, message.CreatedAt });
        builder.HasIndex(message => new { message.SentAt, message.NextAttemptAt, message.CreatedAt })
            .HasFilter("sent_at IS NULL AND failed_at IS NULL");
    }
}
