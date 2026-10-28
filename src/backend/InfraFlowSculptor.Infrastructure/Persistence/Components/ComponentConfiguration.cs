using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Components;

public sealed class ComponentConfiguration : IEntityTypeConfiguration<ComponentAggregate>
{
    public void Configure(EntityTypeBuilder<ComponentAggregate> builder)
    {
        builder.ToTable("components");
        builder.HasKey(component => component.Id).HasName("pk_components");
        builder.Property(component => component.Id).ValueGeneratedNever();
        builder.Property(component => component.ProjectId).IsRequired();
        builder.Property(component => component.OrganizationId).IsRequired();
        builder.Property(component => component.Name).HasMaxLength(60).IsRequired();
        builder.Property(component => component.NameKey).HasMaxLength(60).IsRequired();
        builder.Property(component => component.Code).HasMaxLength(20).IsRequired();
        builder.Property(component => component.CodeKey).HasMaxLength(20).IsRequired();
        builder.Property(component => component.Description).HasMaxLength(1000).IsRequired();
        builder.Property(component => component.Mode).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(component => component.TargetsAllEnvironments).IsRequired();
        builder.Property(component => component.TargetEnvironmentIds)
            .HasColumnName("target_environment_ids")
            .HasColumnType("uuid[]")
            .HasDefaultValueSql("ARRAY[]::uuid[]")
            .IsRequired();
        builder.Property(component => component.RemovedResourceBehavior)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(component => component.DeletionProtectionEnabled).IsRequired();
        builder.Property(component => component.Version).IsConcurrencyToken();
        builder.Property(component => component.FirstPublishedAt);
        builder.HasIndex(component => new { component.ProjectId, component.NameKey })
            .IsUnique()
            .HasDatabaseName("ux_components_project_name");
        builder.HasIndex(component => new { component.ProjectId, component.CodeKey })
            .IsUnique()
            .HasDatabaseName("ux_components_project_code");
        builder.HasOne<ProjectAggregate>()
            .WithMany()
            .HasForeignKey(component => component.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsOne(component => component.SingleTarget, target =>
        {
            target.ToTable("component_single_targets");
            target.WithOwner().HasForeignKey("ComponentId");
            target.HasKey("ComponentId").HasName("pk_component_single_targets");
            target.Property(value => value.Code).HasMaxLength(8).IsRequired();
            target.Property(value => value.SubscriptionId).IsRequired();
            target.Property(value => value.Region).HasMaxLength(64).IsRequired();
            target.Property(value => value.DeploymentConnection).HasMaxLength(100).IsRequired();
            target.Property(value => value.IsProtected).IsRequired();
            target.Property(value => value.Approvers).HasColumnType("text[]").IsRequired();
            target.Property(value => value.Runners).HasColumnType("text[]").IsRequired();
        });
        builder.Navigation(component => component.SingleTarget).IsRequired(false);

        builder.OwnsMany(component => component.Tags, tags =>
        {
            tags.ToTable("component_tags");
            tags.WithOwner().HasForeignKey("ComponentId");
            tags.HasKey("ComponentId", nameof(ComponentTag.Key)).HasName("pk_component_tags");
            tags.Property(tag => tag.Key).HasMaxLength(512).IsRequired();
            tags.Property(tag => tag.Value).HasMaxLength(256).IsRequired();
        });
        builder.Navigation(component => component.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(component => component.ResourceGroups, groups =>
        {
            groups.ToTable("resource_groups");
            groups.WithOwner().HasForeignKey("ComponentId");
            groups.HasKey("ComponentId", nameof(ResourceGroup.Id)).HasName("pk_resource_groups");
            groups.Property(group => group.Id).ValueGeneratedNever();
            groups.Property(group => group.ProjectId).IsRequired();
            groups.Property(group => group.OrganizationId).IsRequired();
            groups.Property(group => group.Name).HasMaxLength(40).IsRequired();
            groups.Property(group => group.Region).HasMaxLength(64);
            groups.Property(group => group.Version).IsConcurrencyToken();
            groups.HasIndex("ComponentId", nameof(ResourceGroup.Name))
                .IsUnique()
                .HasDatabaseName("ux_resource_groups_component_name");

            groups.OwnsMany(group => group.Tags, tags =>
            {
                tags.ToTable("resource_group_tags");
                tags.WithOwner().HasForeignKey("ComponentId", "ResourceGroupId");
                tags.HasKey("ComponentId", "ResourceGroupId", nameof(ResourceGroupTag.Key))
                    .HasName("pk_resource_group_tags");
                tags.Property(tag => tag.Key).HasMaxLength(512).IsRequired();
                tags.Property(tag => tag.Value).HasMaxLength(256).IsRequired();
            });
            groups.Navigation(group => group.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);

            groups.OwnsMany(group => group.ForcedNames, forcedNames =>
            {
                forcedNames.ToTable("resource_group_name_overrides");
                forcedNames.WithOwner().HasForeignKey("ComponentId", "ResourceGroupId");
                forcedNames.HasKey("ComponentId", "ResourceGroupId", nameof(ResourceGroupNameOverride.EnvironmentId))
                    .HasName("pk_resource_group_name_overrides");
                forcedNames.Property(value => value.Name).HasMaxLength(90).IsRequired();
            });
            groups.Navigation(group => group.ForcedNames).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(component => component.ResourceGroups).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
