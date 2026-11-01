using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Components;

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSettingAggregate>
{
    public void Configure(EntityTypeBuilder<AppSettingAggregate> builder)
    {
        builder.ToTable("app_settings");
        builder.HasKey(setting => setting.Id).HasName("pk_app_settings");
        builder.Property(setting => setting.Id).ValueGeneratedNever();
        builder.Property(setting => setting.ProjectId).IsRequired();
        builder.Property(setting => setting.OrganizationId).IsRequired();
        builder.Property(setting => setting.ComponentId).IsRequired();
        builder.Property(setting => setting.ApplicationResourceId).IsRequired();
        builder.Property(setting => setting.Name).HasMaxLength(256).IsRequired();
        builder.Property(setting => setting.NameKey).HasMaxLength(256).IsRequired();
        builder.Property(setting => setting.SourceKind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(setting => setting.SourceResourceId);
        builder.Property(setting => setting.OutputName).HasMaxLength(128);
        builder.Property(setting => setting.KeyVaultResourceId);
        builder.Property(setting => setting.SecretName).HasMaxLength(127);
        builder.Property(setting => setting.SecretNameKey).HasMaxLength(127);
        builder.Property(setting => setting.SecretFeedKind).HasConversion<string>().HasMaxLength(32);
        builder.Property(setting => setting.PipelineVariableName).HasMaxLength(128);
        builder.Property(setting => setting.PipelineVariableNameKey).HasMaxLength(128);
        builder.Property(setting => setting.IdentityResourceId);
        builder.Property(setting => setting.SecretValueDeclaredNonSecret).IsRequired();
        builder.Property(setting => setting.Version).IsConcurrencyToken();
        builder.HasIndex(setting => new { setting.ApplicationResourceId, setting.NameKey })
            .IsUnique()
            .HasDatabaseName("ux_app_settings_application_name");
        builder.HasIndex(setting => new { setting.ProjectId, setting.PipelineVariableNameKey })
            .IsUnique()
            .HasFilter("\"pipeline_variable_name_key\" IS NOT NULL")
            .HasDatabaseName("ux_app_settings_project_pipeline_variable");
        builder.HasIndex(setting => new { setting.KeyVaultResourceId, setting.SecretNameKey })
            .IsUnique()
            .HasFilter("\"secret_feed_kind\" = 'PipelineSecret'")
            .HasDatabaseName("ux_app_settings_pipeline_secret_writer");
        builder.HasOne<ProjectAggregate>()
            .WithMany()
            .HasForeignKey(setting => setting.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ComponentAggregate>()
            .WithMany()
            .HasForeignKey(setting => setting.ComponentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsOne(setting => setting.LiteralSource, literal =>
        {
            literal.ToTable("app_setting_literals");
            literal.WithOwner().HasForeignKey("AppSettingId");
            literal.HasKey("AppSettingId").HasName("pk_app_setting_literals");
            literal.Property(value => value.Value).HasColumnName("value").HasColumnType("text").IsRequired();

            literal.OwnsMany(value => value.Overrides, overrides =>
            {
                overrides.ToTable("app_setting_literal_overrides");
                overrides.WithOwner().HasForeignKey("AppSettingId");
                overrides.HasKey("AppSettingId", nameof(AppSettingLiteralOverride.TargetId))
                    .HasName("pk_app_setting_literal_overrides");
                overrides.Property(value => value.TargetId).HasColumnName("target_id").IsRequired();
                overrides.Property(value => value.Value).HasColumnName("value").HasColumnType("text").IsRequired();
            });
            literal.Navigation(value => value.Overrides).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(setting => setting.LiteralSource).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
