using InfraFlowSculptor.Domain.OrganizationAggregate;
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
        builder.HasOne<OrganizationAggregate>()
            .WithMany()
            .HasForeignKey(project => project.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

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
    }
}
