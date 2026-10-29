using InfraFlowSculptor.Domain.ComponentAggregate;
using InfraFlowSculptor.Domain.ProjectAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraFlowSculptor.Infrastructure.Persistence.Components;

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<ApplicationAggregate>
{
    public void Configure(EntityTypeBuilder<ApplicationAggregate> builder)
    {
        builder.ToTable("applications");
        builder.HasKey(application => application.Id).HasName("pk_applications");
        builder.Property(application => application.Id).ValueGeneratedNever();
        builder.Property(application => application.ProjectId).IsRequired();
        builder.Property(application => application.OrganizationId).IsRequired();
        builder.Property(application => application.ComponentId).IsRequired();
        builder.Property(application => application.ResourceId).IsRequired();
        builder.Property(application => application.Name).HasMaxLength(40).IsRequired();
        builder.Property(application => application.NameKey).HasMaxLength(40).IsRequired();
        builder.Property(application => application.CodeSourcePath).HasMaxLength(512).IsRequired();
        builder.Property(application => application.DockerfilePath).HasMaxLength(512).IsRequired();
        builder.Property(application => application.BuildContextPath).HasMaxLength(512).IsRequired();
        builder.Property(application => application.ImageRepository).HasMaxLength(256).IsRequired();
        builder.Property(application => application.HealthCheckPath).HasMaxLength(2048);
        builder.Property(application => application.DeploymentStrategy).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(application => application.BuildRegistryEnvironmentId);
        builder.Property(application => application.Version).IsConcurrencyToken();
        builder.HasIndex(application => new { application.ComponentId, application.NameKey })
            .IsUnique()
            .HasDatabaseName("ux_applications_component_name");
        builder.HasIndex(application => application.ResourceId)
            .IsUnique()
            .HasDatabaseName("ux_applications_resource");
        builder.HasOne<ProjectAggregate>()
            .WithMany()
            .HasForeignKey(application => application.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ComponentAggregate>()
            .WithMany()
            .HasForeignKey(application => application.ComponentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(application => application.Steps, steps =>
        {
            steps.ToTable("application_steps");
            steps.WithOwner().HasForeignKey("ApplicationId");
            steps.HasKey("ApplicationId", nameof(ApplicationStepDefinition.Name))
                .HasName("pk_application_steps");
            steps.Property(step => step.Name).HasMaxLength(64).IsRequired();
            steps.Property(step => step.Enabled).IsRequired();
            steps.Property(step => step.Blocking).IsRequired();
            steps.Property(step => step.Position).HasConversion<string>().HasMaxLength(32).IsRequired();

            steps.OwnsMany(step => step.Parameters, parameters =>
            {
                parameters.ToTable("application_step_parameters");
                parameters.WithOwner().HasForeignKey("ApplicationId", "StepName");
                parameters.HasKey("ApplicationId", "StepName", nameof(ApplicationStepParameter.Name))
                    .HasName("pk_application_step_parameters");
                parameters.Property(parameter => parameter.Name).HasMaxLength(64).IsRequired();
                parameters.Property(parameter => parameter.Value).HasMaxLength(4096).IsRequired();
            });
            steps.Navigation(step => step.Parameters).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(application => application.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
