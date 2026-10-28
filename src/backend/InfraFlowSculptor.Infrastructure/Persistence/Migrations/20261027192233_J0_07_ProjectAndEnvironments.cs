using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraFlowSculptor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [SuppressMessage("Naming", "CA1707", Justification = "The migration identifier follows the J0-07 plan ID.")]
    [SuppressMessage("Performance", "CA1861", Justification = "EF-generated composite column arrays are migration metadata.")]
    public partial class J0_07_ProjectAndEnvironments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "catalog_version",
                table: "projects",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ci_platform",
                table: "projects",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "AzureDevOps");

            migrationBuilder.AddColumn<Guid>(
                name: "coordinator_user_profile_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "default_reviewers",
                table: "projects",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<string[]>(
                name: "default_runners",
                table: "projects",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "projects",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "first_published_at",
                table: "projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "infrastructure_language",
                table: "projects",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Bicep");

            migrationBuilder.AddColumn<int>(
                name: "model_version",
                table: "projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "module_source",
                table: "projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "AVM registre public");

            migrationBuilder.AddColumn<bool>(
                name: "system_tags_enabled",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "projects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            migrationBuilder.Sql("""
                UPDATE projects AS project
                SET tenant_id = COALESCE((
                    SELECT profile.tenant_id
                    FROM project_role_assignments AS assignment
                    JOIN user_profiles AS profile ON profile.id = assignment.principal_id
                    WHERE assignment.project_id = project.id AND assignment.role = 7
                    ORDER BY assignment.principal_id
                    LIMIT 1
                ), '00000000-0000-0000-0000-000000000000'::uuid),
                catalog_version = '2026.10-pilot',
                infrastructure_language = 'Bicep',
                ci_platform = 'AzureDevOps',
                module_source = 'AVM registre public',
                system_tags_enabled = TRUE,
                model_version = 0;
                """);

            migrationBuilder.CreateTable(
                name: "project_environments",
                columns: table => new
                {
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    region = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    deployment_connection = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_protected = table.Column<bool>(type: "boolean", nullable: false),
                    approvers = table.Column<string[]>(type: "text[]", nullable: false),
                    runners = table.Column<string[]>(type: "text[]", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    first_published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_environments", x => new { x.project_id, x.environment_id });
                    table.ForeignKey(
                        name: "fk_project_environments_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_tags",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_tags", x => new { x.project_id, x.key });
                    table.ForeignKey(
                        name: "fk_project_tags_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_environment_tags",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_environment_tags", x => new { x.project_id, x.environment_id, x.key });
                    table.ForeignKey(
                        name: "fk_project_environment_tags_project_environments_project_id_en",
                        columns: x => new { x.project_id, x.environment_id },
                        principalTable: "project_environments",
                        principalColumns: new[] { "project_id", "environment_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_project_environments_project_position",
                table: "project_environments",
                columns: new[] { "project_id", "position" },
                unique: true);

            migrationBuilder.Sql("DROP INDEX ux_project_environments_project_position; ALTER TABLE project_environments ADD CONSTRAINT uq_project_environments_position UNIQUE (project_id, position) DEFERRABLE INITIALLY DEFERRED;");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ux_projects_organization_name_lower
                    ON projects (organization_id, lower(name));
                CREATE UNIQUE INDEX ux_projects_organization_code_lower
                    ON projects (organization_id, lower(code));
                CREATE UNIQUE INDEX ux_project_environments_project_name_lower
                    ON project_environments (project_id, lower(name));
                CREATE UNIQUE INDEX ux_project_environments_project_code_lower
                    ON project_environments (project_id, lower(code));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ux_project_environments_project_code_lower;
                DROP INDEX IF EXISTS ux_project_environments_project_name_lower;
                DROP INDEX IF EXISTS ux_projects_organization_code_lower;
                DROP INDEX IF EXISTS ux_projects_organization_name_lower;
                """);
            migrationBuilder.DropTable(
                name: "project_environment_tags");

            migrationBuilder.DropTable(
                name: "project_tags");

            migrationBuilder.DropTable(
                name: "project_environments");

            migrationBuilder.DropColumn(
                name: "catalog_version",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "ci_platform",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "coordinator_user_profile_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "default_reviewers",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "default_runners",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "description",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "first_published_at",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "infrastructure_language",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "model_version",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "module_source",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "system_tags_enabled",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "projects");
        }
    }
}
