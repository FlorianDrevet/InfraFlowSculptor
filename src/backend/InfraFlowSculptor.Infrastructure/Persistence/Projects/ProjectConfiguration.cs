using InfraFlowSculptor.Domain.OrganizationAggregate;
using InfraFlowSculptor.Domain.GitConnectionAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Projects;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<ProjectAggregate>
{
    public void Configure(EntityTypeBuilder<ProjectAggregate> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(project => project.Id).HasName("pk_projects");
        builder.Property(project => project.OrganizationId).IsRequired();
        builder.Property(project => project.Name).HasMaxLength(80).IsRequired();
        builder.Property(project => project.Code).HasMaxLength(10).IsRequired();
        builder.Property(project => project.Version).IsConcurrencyToken();
        builder.Property(project => project.DeletedAt);
        builder.Property(project => project.TenantId).IsRequired();
        builder.Property(project => project.Description).HasMaxLength(1000).IsRequired();
        builder.Property(project => project.InfrastructureLanguage).HasMaxLength(32).HasDefaultValue("Bicep").IsRequired();
        builder.Property(project => project.CiPlatform).HasMaxLength(32).HasDefaultValue("AzureDevOps").IsRequired();
        builder.Property(project => project.ModuleSource).HasMaxLength(100).HasDefaultValue("AVM registre public").IsRequired();
        builder.Property(project => project.CatalogVersion).HasMaxLength(64).IsRequired();
        builder.Property(project => project.NamingPreset).HasMaxLength(16).HasDefaultValue(ProjectNamingPresets.Compact).IsRequired();
        builder.Property(project => project.DefaultNameTemplate).HasMaxLength(200).HasDefaultValue("{abbr}-{project}-{name}-{env}").IsRequired();
        builder.Property(project => project.ResourceGroupNameTemplate).HasMaxLength(200).HasDefaultValue("rg-{project}-{component}-{name}-{env}").IsRequired();
        builder.Ignore(project => project.PublishingPreset);
        builder.Property(project => project.SystemTagsEnabled).HasDefaultValue(true).IsRequired();
        builder.Property(project => project.ModelVersion).HasDefaultValue(0).IsRequired();
        builder.Property(project => project.FirstPublishedAt);
        builder.Property(project => project.CoordinatorUserProfileId);
        builder.Property(project => project.DefaultRunners)
            .HasColumnType("text[]")
            .HasDefaultValueSql("ARRAY[]::text[]")
            .IsRequired();
        builder.Property(project => project.DefaultReviewers)
            .HasColumnType("text[]")
            .HasDefaultValueSql("ARRAY[]::text[]")
            .IsRequired();
        builder.HasOne<OrganizationAggregate>()
            .WithMany()
            .HasForeignKey(project => project.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsOne(project => project.PublicationPlan, plan =>
        {
            plan.Property(value => value.Preset)
                .HasConversion<string>()
                .HasColumnName("publishing_preset")
                .HasMaxLength(32)
                .HasDefaultValue(PublishPreset.MonoRepo)
                .IsRequired();
            plan.Property(value => value.DefaultInfrastructureDestinationId)
                .HasColumnName("publish_default_infrastructure_destination_id");
            plan.Property(value => value.DefaultApplicationsDestinationId)
                .HasColumnName("publish_default_applications_destination_id");
            plan.Property(value => value.DefaultSourceCodeDestinationId)
                .HasColumnName("publish_default_source_code_destination_id");

            plan.OwnsMany(value => value.Destinations, destinations =>
            {
                destinations.ToTable("publish_plan_destinations");
                destinations.WithOwner().HasForeignKey("ProjectId");
                destinations.HasKey("ProjectId", nameof(PublishPlanDestination.Id))
                    .HasName("pk_publish_plan_destinations");
                destinations.Property(value => value.Id)
                    .HasColumnName("destination_id")
                    .ValueGeneratedNever();
                destinations.Property(value => value.GitConnectionId).IsRequired();
                destinations.Property(value => value.ProviderRepositoryId).HasMaxLength(300).IsRequired();
                destinations.Property(value => value.BasePath).HasMaxLength(200).IsRequired();
                destinations.HasOne<GitConnectionAggregate>()
                    .WithMany()
                    .HasForeignKey(value => value.GitConnectionId)
                    .OnDelete(DeleteBehavior.Restrict);
                destinations.HasIndex("ProjectId", nameof(PublishPlanDestination.GitConnectionId),
                        nameof(PublishPlanDestination.ProviderRepositoryId), nameof(PublishPlanDestination.BasePath))
                    .IsUnique()
                    .HasDatabaseName("ux_publish_plan_destinations_repository_path");
            });
            plan.Navigation(value => value.Destinations).UsePropertyAccessMode(PropertyAccessMode.Field);

            plan.OwnsMany(value => value.Assignments, assignments =>
            {
                assignments.ToTable("publish_plan_assignments");
                assignments.WithOwner().HasForeignKey("ProjectId");
                assignments.HasKey("ProjectId", nameof(PublishPlanAssignment.ComponentId), nameof(PublishPlanAssignment.Part))
                    .HasName("pk_publish_plan_assignments");
                assignments.Property(value => value.ComponentId).HasColumnName("component_id").IsRequired();
                assignments.Property(value => value.Part).HasConversion<string>().HasMaxLength(32).IsRequired();
                assignments.Property(value => value.DestinationId).HasColumnName("destination_id").IsRequired();
            });
            plan.Navigation(value => value.Assignments).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(project => project.PublicationPlan).IsRequired();

        builder.OwnsMany(project => project.RoleAssignments, assignments =>
        {
            assignments.ToTable("project_role_assignments");
            assignments.WithOwner().HasForeignKey("ProjectId");
            assignments.HasKey(
                    "ProjectId",
                    nameof(ProjectRoleAssignment.Principal),
                    nameof(ProjectRoleAssignment.Role),
                    nameof(ProjectRoleAssignment.Scope))
                .HasName("pk_project_role_assignments");
            assignments.Property(assignment => assignment.OrganizationId).IsRequired();
            assignments.Property(assignment => assignment.Principal)
                .HasColumnName("principal_id")
                .IsRequired();
            assignments.Property(assignment => assignment.Role)
                .HasConversion<int>()
                .IsRequired();
            assignments.Property(assignment => assignment.Scope)
                .HasConversion<int>()
                .IsRequired();
            assignments.HasIndex(
                    nameof(ProjectRoleAssignment.OrganizationId),
                    nameof(ProjectRoleAssignment.Principal))
                .HasDatabaseName("ix_project_role_assignments_organization_principal");
        });

        builder.Navigation(project => project.RoleAssignments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(project => project.Tags, tags =>
        {
            tags.ToTable("project_tags");
            tags.WithOwner().HasForeignKey("ProjectId");
            tags.HasKey("ProjectId", nameof(ProjectTag.Key)).HasName("pk_project_tags");
            tags.Property(tag => tag.Key).HasMaxLength(512).IsRequired();
            tags.Property(tag => tag.Value).HasMaxLength(256).IsRequired();
        });
        builder.Navigation(project => project.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(project => project.Environments, environments =>
        {
            environments.ToTable("project_environments");
            environments.WithOwner().HasForeignKey("ProjectId");
            environments.HasKey("ProjectId", nameof(ProjectEnvironment.Id)).HasName("pk_project_environments");
            environments.Property(environment => environment.Id)
                .HasColumnName("environment_id")
                .ValueGeneratedNever();
            environments.Property(environment => environment.Version).IsRequired();
            environments.Property(environment => environment.Name).HasMaxLength(50).IsRequired();
            environments.Property(environment => environment.Code).HasMaxLength(8).IsRequired();
            environments.Property(environment => environment.Position).IsRequired();
            environments.Property(environment => environment.SubscriptionId).IsRequired();
            environments.Property(environment => environment.Region).HasMaxLength(64).IsRequired();
            environments.Property(environment => environment.DeploymentConnection).HasMaxLength(100).IsRequired();
            environments.Property(environment => environment.IsProtected).IsRequired();
            environments.Property(environment => environment.Approvers).HasColumnType("text[]").IsRequired();
            environments.Property(environment => environment.Runners).HasColumnType("text[]").IsRequired();
            environments.Property(environment => environment.Description).HasMaxLength(500).IsRequired();
            environments.Property(environment => environment.FirstPublishedAt);
            environments.HasIndex("ProjectId", nameof(ProjectEnvironment.Position))
                .IsUnique()
                .HasDatabaseName("ux_project_environments_project_position");

            environments.OwnsMany(environment => environment.Tags, tags =>
            {
                tags.ToTable("project_environment_tags");
                tags.WithOwner().HasForeignKey("ProjectId", "EnvironmentId");
                tags.HasKey("ProjectId", "EnvironmentId", nameof(ProjectTag.Key))
                    .HasName("pk_project_environment_tags");
                tags.Property(tag => tag.Key).HasMaxLength(512).IsRequired();
                tags.Property(tag => tag.Value).HasMaxLength(256).IsRequired();
            });
            environments.Navigation(environment => environment.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(project => project.Environments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(project => project.NamingOverrides, overrides =>
        {
            overrides.ToTable("project_resource_type_naming_overrides");
            overrides.WithOwner().HasForeignKey("ProjectId");
            overrides.HasKey("ProjectId", nameof(ResourceTypeNamingOverride.Type))
                .HasName("pk_project_resource_type_naming_overrides");
            overrides.Property(value => value.Type).HasMaxLength(100).IsRequired();
            overrides.Property(value => value.Template).HasMaxLength(200);
            overrides.Property(value => value.Abbreviation).HasMaxLength(16);
        });
        builder.Navigation(project => project.NamingOverrides).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
