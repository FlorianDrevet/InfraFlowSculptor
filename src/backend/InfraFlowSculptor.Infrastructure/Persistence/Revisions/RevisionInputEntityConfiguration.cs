using InfraFlowSculptor.Application.Common.Persistence;
using InfraFlowSculptor.Domain.RevisionAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Revisions;

public sealed class RevisionInputEntityConfiguration : IEntityTypeConfiguration<RevisionInputEntity>
{
    public void Configure(EntityTypeBuilder<RevisionInputEntity> builder)
    {
        builder.ToTable("revision_inputs");
        builder.HasKey(input => input.RevisionId).HasName("pk_revision_inputs");
        builder.Property(input => input.RevisionId).ValueGeneratedNever();
        builder.Property(input => input.ModelVersion).IsRequired();
        builder.Property(input => input.CatalogVersion).HasMaxLength(64).IsRequired();
        builder.Property(input => input.InfrastructureLanguage).HasMaxLength(32).IsRequired();
        builder.Property(input => input.CiPlatform).HasMaxLength(32).IsRequired();
        builder.Property(input => input.Snapshot).HasColumnType("text").IsRequired();
        builder.Property(input => input.Sha256).HasMaxLength(64).IsRequired();
        builder.HasOne<RevisionAggregate>()
            .WithOne()
            .HasForeignKey<RevisionInputEntity>(input => input.RevisionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
