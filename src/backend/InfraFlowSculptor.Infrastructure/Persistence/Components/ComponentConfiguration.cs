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
        builder.Property(component => component.DefaultNameTemplate).HasMaxLength(200);
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
        builder.Property(component => component.DefaultLogAnalyticsResourceId);
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
            target.Property(value => value.Id).ValueGeneratedNever().IsRequired();
            target.HasIndex(value => value.Id).IsUnique().HasDatabaseName("ux_component_single_targets_id");
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

        builder.OwnsMany(component => component.NamingOverrides, overrides =>
        {
            overrides.ToTable("component_resource_type_naming_overrides");
            overrides.WithOwner().HasForeignKey("ComponentId");
            overrides.HasKey("ComponentId", nameof(ResourceTypeNamingOverride.Type))
                .HasName("pk_component_resource_type_naming_overrides");
            overrides.Property(value => value.Type).HasMaxLength(100).IsRequired();
            overrides.Property(value => value.Template).HasMaxLength(200);
            overrides.Property(value => value.Abbreviation).HasMaxLength(16);
        });
        builder.Navigation(component => component.NamingOverrides).UsePropertyAccessMode(PropertyAccessMode.Field);

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

        builder.OwnsMany(component => component.Resources, resources =>
        {
            resources.ToTable("resources");
            resources.WithOwner().HasForeignKey("ComponentId");
            resources.HasKey("ComponentId", nameof(Resource.Id)).HasName("pk_resources");
            resources.Property(resource => resource.Id).ValueGeneratedNever();
            resources.Property(resource => resource.ProjectId).IsRequired();
            resources.Property(resource => resource.OrganizationId).IsRequired();
            resources.Property(resource => resource.ResourceGroupId).IsRequired();
            resources.Property(resource => resource.Type).HasMaxLength(100).IsRequired();
            resources.Property(resource => resource.LogicalName).HasMaxLength(40).IsRequired();
            resources.Property(resource => resource.Description).HasMaxLength(500).IsRequired();
            resources.Property(resource => resource.IsExisting).IsRequired();
            resources.Property(resource => resource.Version).IsConcurrencyToken();
            resources.HasIndex("ComponentId", nameof(Resource.Type), nameof(Resource.LogicalName))
                .IsUnique()
                .HasDatabaseName("ux_resources_component_type_logical_name");

            resources.OwnsMany(resource => resource.Values, values =>
            {
                values.ToTable("resource_value_nodes", table =>
                {
                    table.HasCheckConstraint(
                        "ck_resource_value_nodes_shape",
                        "(parent_id IS NULL AND property_name IS NOT NULL AND member_name IS NULL AND position IS NULL) OR (parent_id IS NOT NULL AND property_name IS NULL AND target_id IS NULL AND ((member_name IS NOT NULL AND position IS NULL) OR (member_name IS NULL AND position IS NOT NULL AND position >= 0)))");
                    table.HasCheckConstraint(
                        "ck_resource_value_nodes_typed_value",
                        "(kind = 'Absent' AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NULL) OR (kind = 'Textual' AND string_value IS NOT NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NULL) OR (kind = 'Truth' AND string_value IS NULL AND boolean_value IS NOT NULL AND integer_value IS NULL AND number_value IS NULL) OR (kind = 'WholeNumber' AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NOT NULL AND number_value IS NULL) OR (kind = 'DecimalNumber' AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NOT NULL) OR (kind IN ('Sequence', 'Map') AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NULL)");
                });
                values.WithOwner().HasForeignKey("ComponentId", "ResourceId");
                values.HasKey("ComponentId", "ResourceId", nameof(ResourceValueNode.Id))
                    .HasName("pk_resource_value_nodes");
                values.Property(value => value.Id).ValueGeneratedNever();
                values.Property(value => value.TargetId);
                values.Property(value => value.ParentId);
                values.Property(value => value.PropertyName).HasMaxLength(256);
                values.Property(value => value.MemberName).HasMaxLength(256);
                values.Property(value => value.Position);
                values.Property(value => value.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
                values.Property(value => value.StringValue).HasColumnType("text");
                values.Property(value => value.BooleanValue);
                values.Property(value => value.IntegerValue);
                values.Property(value => value.NumberValue).HasColumnType("numeric");
                values.HasIndex("ComponentId", "ResourceId", nameof(ResourceValueNode.ParentId), nameof(ResourceValueNode.MemberName))
                    .IsUnique()
                    .HasFilter("member_name IS NOT NULL")
                    .HasDatabaseName("ux_resource_value_nodes_parent_member");
                values.HasIndex("ComponentId", "ResourceId", nameof(ResourceValueNode.ParentId), nameof(ResourceValueNode.Position))
                    .IsUnique()
                    .HasFilter("position IS NOT NULL")
                    .HasDatabaseName("ux_resource_value_nodes_parent_position");
                values.HasIndex("ComponentId", "ResourceId", nameof(ResourceValueNode.PropertyName))
                    .IsUnique()
                    .HasFilter("parent_id IS NULL AND target_id IS NULL AND property_name IS NOT NULL")
                    .HasDatabaseName("ux_resource_value_nodes_base_property");
                values.HasIndex("ComponentId", "ResourceId", nameof(ResourceValueNode.TargetId), nameof(ResourceValueNode.PropertyName))
                    .IsUnique()
                    .HasFilter("parent_id IS NULL AND target_id IS NOT NULL AND property_name IS NOT NULL")
                    .HasDatabaseName("ux_resource_value_nodes_target_property");
            });
            resources.Navigation(resource => resource.Values).UsePropertyAccessMode(PropertyAccessMode.Field);

            resources.OwnsMany(resource => resource.Presences, presences =>
            {
                presences.ToTable("resource_presences");
                presences.WithOwner().HasForeignKey("ComponentId", "ResourceId");
                presences.HasKey("ComponentId", "ResourceId", nameof(ResourcePresence.TargetId))
                    .HasName("pk_resource_presences");
                presences.Property(presence => presence.TargetId).ValueGeneratedNever();
                presences.Property(presence => presence.IsPresent).IsRequired();
            });
            resources.Navigation(resource => resource.Presences).UsePropertyAccessMode(PropertyAccessMode.Field);

            resources.OwnsMany(resource => resource.ExistingIds, existingIds =>
            {
                existingIds.ToTable("existing_resource_ids");
                existingIds.WithOwner().HasForeignKey("ComponentId", "ResourceId");
                existingIds.HasKey("ComponentId", "ResourceId", nameof(ResourceExistingId.TargetId))
                    .HasName("pk_existing_resource_ids");
                existingIds.Property(value => value.TargetId).ValueGeneratedNever();
                existingIds.Property(value => value.Value).HasMaxLength(2048).IsRequired();
            });
            resources.Navigation(resource => resource.ExistingIds).UsePropertyAccessMode(PropertyAccessMode.Field);

            resources.OwnsMany(resource => resource.ForcedNames, forcedNames =>
            {
                forcedNames.ToTable("resource_name_overrides");
                forcedNames.WithOwner().HasForeignKey("ComponentId", "ResourceId");
                forcedNames.HasKey("ComponentId", "ResourceId", nameof(ResourceNameOverride.TargetId))
                    .HasName("pk_resource_name_overrides");
                forcedNames.Property(value => value.TargetId).ValueGeneratedNever();
                forcedNames.Property(value => value.Name).HasMaxLength(90).IsRequired();
            });
            resources.Navigation(resource => resource.ForcedNames).UsePropertyAccessMode(PropertyAccessMode.Field);

            resources.OwnsMany(resource => resource.Tags, tags =>
            {
                tags.ToTable("resource_tags");
                tags.WithOwner().HasForeignKey("ComponentId", "ResourceId");
                tags.HasKey("ComponentId", "ResourceId", nameof(ResourceTag.Key)).HasName("pk_resource_tags");
                tags.Property(tag => tag.Key).HasMaxLength(512).ValueGeneratedNever().IsRequired();
                tags.Property(tag => tag.Value).HasMaxLength(256).IsRequired();
            });
            resources.Navigation(resource => resource.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);

            resources.OwnsMany(resource => resource.Children, children =>
            {
                children.ToTable("resource_children");
                children.WithOwner().HasForeignKey("ComponentId", "ResourceId");
                children.HasKey("ComponentId", "ResourceId", nameof(ResourceChild.Id)).HasName("pk_resource_children");
                children.Property(child => child.Id).ValueGeneratedNever();
                children.Property(child => child.Type).HasMaxLength(100).IsRequired();
                children.Property(child => child.Name).HasMaxLength(90).IsRequired();
                children.Property(child => child.NameKey).HasMaxLength(90).IsRequired();
                children.HasIndex("ComponentId", "ResourceId", nameof(ResourceChild.NameKey))
                    .IsUnique()
                    .HasDatabaseName("ux_resource_children_name");

                children.OwnsMany(child => child.Values, childValues =>
                {
                    childValues.ToTable("resource_child_value_nodes", table =>
                    {
                        table.HasCheckConstraint(
                            "ck_resource_child_value_nodes_shape",
                            "parent_id IS NULL AND property_name IS NOT NULL AND member_name IS NULL AND position IS NULL AND target_id IS NULL OR parent_id IS NOT NULL AND property_name IS NULL AND target_id IS NULL AND ((member_name IS NOT NULL AND position IS NULL) OR (member_name IS NULL AND position IS NOT NULL AND position >= 0))");
                        table.HasCheckConstraint(
                            "ck_resource_child_value_nodes_typed_value",
                            "(kind = 'Absent' AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NULL) OR (kind = 'Textual' AND string_value IS NOT NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NULL) OR (kind = 'Truth' AND string_value IS NULL AND boolean_value IS NOT NULL AND integer_value IS NULL AND number_value IS NULL) OR (kind = 'WholeNumber' AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NOT NULL AND number_value IS NULL) OR (kind = 'DecimalNumber' AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NOT NULL) OR (kind IN ('Sequence', 'Map') AND string_value IS NULL AND boolean_value IS NULL AND integer_value IS NULL AND number_value IS NULL)");
                    });
                    childValues.WithOwner().HasForeignKey("ComponentId", "ResourceId", "ResourceChildId");
                    childValues.HasKey("ComponentId", "ResourceId", "ResourceChildId", nameof(ResourceValueNode.Id))
                        .HasName("pk_resource_child_value_nodes");
                    childValues.Property(value => value.Id).ValueGeneratedNever();
                    childValues.Property(value => value.ParentId);
                    childValues.Property(value => value.PropertyName).HasMaxLength(256);
                    childValues.Property(value => value.MemberName).HasMaxLength(256);
                    childValues.Property(value => value.Position);
                    childValues.Property(value => value.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
                    childValues.Property(value => value.StringValue).HasColumnType("text");
                    childValues.Property(value => value.BooleanValue);
                    childValues.Property(value => value.IntegerValue);
                    childValues.Property(value => value.NumberValue).HasColumnType("numeric");
                    childValues.HasIndex("ComponentId", "ResourceId", "ResourceChildId", nameof(ResourceValueNode.ParentId), nameof(ResourceValueNode.MemberName))
                        .IsUnique()
                        .HasFilter("member_name IS NOT NULL")
                        .HasDatabaseName("ux_resource_child_value_nodes_parent_member");
                    childValues.HasIndex("ComponentId", "ResourceId", "ResourceChildId", nameof(ResourceValueNode.ParentId), nameof(ResourceValueNode.Position))
                        .IsUnique()
                        .HasFilter("position IS NOT NULL")
                        .HasDatabaseName("ux_resource_child_value_nodes_parent_position");
                    childValues.HasIndex("ComponentId", "ResourceId", "ResourceChildId", nameof(ResourceValueNode.PropertyName))
                        .IsUnique()
                        .HasFilter("parent_id IS NULL AND property_name IS NOT NULL")
                        .HasDatabaseName("ux_resource_child_value_nodes_property");
                });
                children.Navigation(child => child.Values).UsePropertyAccessMode(PropertyAccessMode.Field);
            });
            resources.Navigation(resource => resource.Children).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(component => component.Resources).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
